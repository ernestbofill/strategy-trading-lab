using System.Text.Json;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// A strategy's own test settings, saved from the Backtest page's <i>Save as defaults</i> (owner only): the years, money and costs its
/// backtest opens on in place of the test defaults, and that <i>Reset</i> goes back to. Which stocks a test runs on (the draft, the list,
/// one stock, the fund) is the test's mode, not a setting, so it isn't saved; nor are the rules and currency, which stay the strategy's.
/// Null fields take the test defaults (<see cref="BacktestRunner.Resolve"/>).
/// </summary>
public record BacktestDefaults(int? Years = null, int? PeriodYears = null, decimal? Budget = null, decimal? AmountPerTrade = null,
    int? MaxTrades = null, decimal? MonthlyLimit = null, decimal? FeePct = null, decimal? SpreadPct = null)
{
    public JsonDocument ToJson() => JsonSerializer.SerializeToDocument(this, JsonSerializerOptions.Web);

    public static BacktestDefaults? From(JsonDocument? json) => json?.Deserialize<BacktestDefaults>(JsonSerializerOptions.Web);

    /// <summary>Why these can't be saved for <paramref name="template"/>, or null: the ranges a test is held to.</summary>
    public string? Error(Template template)
    {
        // Over the test draft, which a strategy's backtest opens on: left out, the years are its 16, which the period must divide.
        var request = new BacktestRequest(template.Key, null, TestStocks.Draft, null, null, Years, PeriodYears, Budget, AmountPerTrade, MaxTrades,
            MonthlyLimit, FeePct, SpreadPct);
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
