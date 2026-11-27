using System.Text.Json;
using System.Text.Json.Serialization;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// A strategy's own test settings, saved from the Backtest page's <i>Save as defaults</i> (owner only): what its backtest opens on
/// in place of the test defaults. The rules and the currency stay the strategy's; a list's symbols are the strategy's at test time,
/// so only one stock keeps its symbol. Null fields take the test defaults (<see cref="BacktestRunner.Resolve"/>).
/// </summary>
public record BacktestDefaults(TestStocks Stocks = TestStocks.Draft, IReadOnlyList<string>? Symbols = null, string? Index = null,
    int? Years = null, int? PeriodYears = null, decimal? Budget = null, decimal? AmountPerTrade = null, int? MaxTrades = null,
    decimal? MonthlyLimit = null, decimal? FeePct = null, decimal? SpreadPct = null)
{
    /// <summary>As stored in <c>strategies.backtest_defaults</c>: camelCase, the stocks by name.</summary>
    static readonly JsonSerializerOptions Stored = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    public JsonDocument ToJson() => JsonSerializer.SerializeToDocument(this, Stored);

    public static BacktestDefaults? From(JsonDocument? json) => json?.Deserialize<BacktestDefaults>(Stored);

    /// <summary>
    /// Why these can't be saved for <paramref name="template"/>, or null: the same ranges a test is held to, one stock with one symbol,
    /// and no symbols for a list (it tests the strategy's own).
    /// </summary>
    public string? Error(Template template)
    {
        if (Stocks == TestStocks.List && Symbols is { Count: > 0 }) return "A list tests the strategy's own stocks: leave the symbols out.";
        // The stock checks need the symbols a test would have; the list's are the strategy's, so one stands in for them.
        var request = new BacktestRequest(template.Key, null, Stocks, Stocks == TestStocks.List ? ["LIST"] : Symbols, Index, Years, PeriodYears,
            Budget, AmountPerTrade, MaxTrades, MonthlyLimit, FeePct, SpreadPct);
        try
        {
            BacktestRunner.Resolve(request, template);
            return null;
        }
        catch (BacktestRunException error)
        {
            return error.Message;
        }
    }
}
