using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// What a push notification carries, as the service worker reads it (frontend <c>public/push-sw.js</c>): a title, the text,
/// the app path it opens, and a tag, so a second notification about the same thing replaces the first.
/// </summary>
public record PushMessage(string Title, string Body, string Url, string Tag)
{
    public string ToJson() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);
}

/// <summary>The words of the push notifications: the email's subject as the title and the signal box as the text (spec: Notifications).</summary>
public static class PushTexts
{
    public static PushMessage SignalAlert(SignalLine s, string locale) => new(
        locale == "ca" ? $"{s.StockName}: es compleixen les teves regles per a {s.StrategyName}" : $"{s.StockName}: your rules for {s.StrategyName} are met",
        EmailTexts.SignalText(s, locale), SignalPath(s), $"signal-{s.SignalId}");

    public static PushMessage SnoozeReminder(SignalLine s, string locale) => new(
        locale == "ca" ? $"Recordatori: {s.StockName}, les teves regles per a {s.StrategyName}" : $"Reminder: {s.StockName}, your rules for {s.StrategyName}",
        EmailTexts.SignalText(s, locale), SignalPath(s), $"signal-{s.SignalId}");

    /// <summary>Replaces the signal's own notification on the device (the same tag).</summary>
    public static PushMessage SignalEnded(SignalLine s, DateOnly endedOn, string locale) => new(
        EmailTexts.EndedTitle(s, locale), EmailTexts.EndedText(s, endedOn, locale), SignalPath(s), $"signal-{s.SignalId}");

    /// <summary>One for every round due that day; it opens the round when there is one, else the dashboard.</summary>
    public static PushMessage AiRoundDue(IReadOnlyList<(Guid StrategyId, string Name)> strategies, DateOnly dueOn, string locale)
    {
        var ca = locale == "ca";
        var names = strategies.Count == 1 ? strategies[0].Name
            : string.Join(", ", strategies.Take(strategies.Count - 1).Select(s => s.Name)) + (ca ? " i " : " and ") + strategies[^1].Name;
        return new(
            ca ? "Ronda d'IA" : "AI round",
            ca ? $"És l'hora de la ronda d'IA de {names}. Són uns dos minuts." : $"Time for the AI round of {names}. It takes about two minutes.",
            strategies.Count == 1 ? $"/strategies/{strategies[0].StrategyId}/ai-round" : "/",
            $"ai-round-{dueOn:yyyy-MM-dd}");
    }

    const int MaxCloseLines = 3;

    /// <summary>
    /// "Getting close": the Manual strategies' stocks that came within the template's getting-close % of their line on the session
    /// (spec: Notifications, Getting close). One stock opens its page; more open the summary, which lists them.
    /// </summary>
    public static PushMessage GettingClose(IReadOnlyList<StockChangeLine> changes, DateOnly session, string locale)
    {
        var ca = locale == "ca";
        var first = changes[0];
        return new(
            changes.Count == 1
                ? ca ? $"{first.StockName} s'acosta a la línia" : $"{first.StockName} is getting close"
                : ca ? $"{changes.Count} accions s'acosten a la línia" : $"{changes.Count} stocks are getting close",
            // A few lines at most: a push payload is capped at about 4 KB, and the summary lists them all.
            string.Join("\n", changes.Take(MaxCloseLines).Select(c => EmailTexts.StockChangeText(c, locale)))
                + (changes.Count > MaxCloseLines ? "\n" + (ca ? $"I {changes.Count - MaxCloseLines} més." : $"And {changes.Count - MaxCloseLines} more.") : ""),
            changes.Count == 1 ? $"/strategies/{first.StrategyId}/stocks/{first.AssetId}" : "/summary",
            $"close-{session:yyyy-MM-dd}");
    }

    /// <summary>Summary push notification: opens the dedicated daily summary page.</summary>
    public static PushMessage Summary(DateOnly day, string locale) => new(
        EmailTexts.OverviewTitleText(day, locale),
        locale == "ca" ? "Què ha canviat avui a les teves estratègies." : "What changed today across your strategies.",
        "/summary",
        $"summary-{day:yyyy-MM-dd}");

    static string SignalPath(SignalLine s) => $"/strategies/{s.StrategyId}/signals/{s.SignalId}";
}
