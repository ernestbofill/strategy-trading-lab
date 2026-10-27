using System.Globalization;
using System.Text;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Ai;

/// <summary>A stock as the prompt lists it and the answer is read against.</summary>
public record AiStock(Guid AssetId, string Symbol, string Name);

/// <summary>One of "What the AI reads": its key (also its column in the answer), title, and what a score measures.</summary>
public record AiSource(string Key, string Title, string Criteria);

/// <summary>
/// The suggested prompt (spec: Review, "The prompt"; AI round, "Copy the prompt"). Three parts: the date line, the instructions
/// (the one part the user can edit) and the answer format with the stocks, which the app always adds so it can read the answer.
/// It never includes anything about the user: no budget, goals or holdings. Pure: data in, text out.
/// </summary>
public static class AiPrompt
{
    /// <summary>The suggested prompt's version: bump it when its words change, so rounds say which one they ran.</summary>
    public const int Version = 1;

    public static readonly IReadOnlyList<AiSource> Sources =
    [
        new("news", "News", "what recent news says about the business: results, products, contracts, lawsuits, management. 50 is neutral."),
        new("mood", "Mood", "how investors, analysts and the press talk about it lately, and whether that tone is getting better or worse. 50 is neutral."),
        new("figures", "Figures", "the latest quarterly figures against the quarters and the year before: revenue growth, margins, cash flow and debt."),
    ];

    /// <summary>The sources a strategy's params switch on, in the prompt's order.</summary>
    public static IReadOnlyList<AiSource> On(AiParams parameters) =>
        [.. Sources.Where(source => source.Key switch
        {
            "news" => parameters.News,
            "mood" => parameters.Mood,
            _ => parameters.Figures,
        })];

    /// <summary>The suggested instructions for these sources: what to score, by which criteria, and what not to say.</summary>
    public static string SuggestedInstructions(IReadOnlyList<AiSource> sources)
    {
        var text = new StringBuilder("Score each stock below from 0 to 100 on:\n");
        foreach (var source in sources) text.Append($"- {source.Title}: {source.Criteria}\n");
        text.Append("and overall, from what you found.\n");
        text.Append("In the reason, give one line and your main sources, with their dates.\n");
        text.Append("Score each stock on its own facts. Don't say which stocks to buy or sell.");
        return text.ToString();
    }

    /// <summary>The whole prompt for a round on <paramref name="today"/>, with the user's instructions if they edited them.</summary>
    public static string Build(AiParams parameters, DateOnly today, IReadOnlyList<AiStock> stocks)
    {
        var sources = On(parameters);
        var period = Period(parameters.EveryDays);
        var text = new StringBuilder();
        text.Append($"Today is {today.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)}. Search the web for news and data from the last {period}, ");
        text.Append("plus each company's latest quarterly figures. Use what you find, not what you remember.\n\n");
        text.Append((parameters.Instructions ?? SuggestedInstructions(sources)).Trim());
        text.Append("\n\nAnswer only with this table, one row per stock below, scores as whole numbers from 0 to 100. ");
        text.Append("In the Stock column, write the stock's symbol as listed below.\n");
        text.Append(TableHeader(sources));
        text.Append("\n\nStocks:\n");
        foreach (var stock in stocks) text.Append($"- {stock.Symbol}: {stock.Name}\n");
        return text.ToString().TrimEnd();
    }

    /// <summary>"day", "5 days", "week", "2 weeks": whole weeks in weeks, so the default prompt reads as it did with a weekly setting.</summary>
    public static string Period(int days) => days switch
    {
        1 => "day",
        7 => "week",
        _ when days % 7 == 0 => $"{days / 7} weeks",
        _ => $"{days} days",
    };

    /// <summary>"| Stock | News | Mood | Figures | Overall | Reason |".</summary>
    public static string TableHeader(IReadOnlyList<AiSource> sources) =>
        $"| Stock | {string.Concat(sources.Select(s => s.Title + " | "))}Overall | Reason |";
}
