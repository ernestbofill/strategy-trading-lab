using System.Text.Json;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// One stock template in one calendar year on the test draft: how many different stocks were candidates on some day, how many got a buy
/// signal, and the buy signals in all. Each year runs from scratch, each stock buying up to its max trades, with no budget (the simulator,
/// ROADMAP item 53, adds the money).
/// </summary>
public record CandidateYear(string Template, int Year, int Candidates, int Signalled, int BuySignals)
{
    /// <summary>The check (docs/backtest.md, *How it is drawn*): 4–5 different stocks a year, read as at least 4 with a buy signal.</summary>
    public bool Passed => Signalled >= CandidateCheck.MinStocks;
}

/// <summary>One drafted stock's prices for a period: from the warm-up to the period's end.</summary>
public record DraftHistory(string Symbol, IReadOnlyList<PriceBar> Bars);

/// <summary>Runs a stock template's rules, at the defaults in use, over each period's drafted stocks, as the app's engine does.</summary>
public static class CandidateCheck
{
    public const int MinStocks = 4;

    public static IReadOnlyList<CandidateYear> Count(string templateKey, JsonDocument parameters, int maxSteps,
        IReadOnlyList<(BacktestPeriod Period, IReadOnlyList<DraftHistory> Stocks)> periods)
    {
        var years = new SortedDictionary<int, (HashSet<string> Candidates, HashSet<string> Signalled, int Signals)>();
        foreach (var (period, stocks) in periods)
        {
            for (var y = period.FirstYear; y <= period.End.Year; y++)
            {
                years.TryAdd(y, ([], [], 0));
                // Each year from scratch (Ernest, 2026-11-25): positions reset on 1 January, so a stock bought in an earlier year and
                // held since still counts when it meets the rules again. The rules read the history before, as at a period's start.
                var (from, to) = (new DateOnly(y, 1, 1), y == period.End.Year ? period.End : new DateOnly(y, 12, 31));
                foreach (var stock in stocks)
                    CountYear(templateKey, parameters, maxSteps, stock, from, to, y == period.FirstYear, years);
            }
        }
        return [.. years.Select(y => new CandidateYear(templateKey, y.Key, y.Value.Candidates.Count, y.Value.Signalled.Count, y.Value.Signals))];
    }

    /// <param name="periodStart">The period's first year: a window open on its first day is a buy the period makes (the simulator buys it), so it counts.</param>
    static void CountYear(string templateKey, JsonDocument parameters, int maxSteps, DraftHistory stock, DateOnly from, DateOnly to, bool periodStart,
        SortedDictionary<int, (HashSet<string> Candidates, HashSet<string> Signalled, int Signals)> years)
    {
        var bars = stock.Bars.Where(b => b.Date <= to).ToList();
        if (bars.Count == 0 || bars[^1].Date < from) return;
        var start = RulesRunner.StartIndex(bars, from);
        // A buy window already open on the last trading day before 1 January: the year's first signal for that step is the same one
        // going on, counted last year, not a new one. Only the count skips it; the run itself starts from scratch on 1 January.
        var openStep = start > 0 && !periodStart ? OpenAtYearEnd(templateKey, parameters, maxSteps, [.. bars.Take(start)]) : null;
        var rules = TemplateRules.Create(templateKey, bars, parameters);
        var days = RulesRunner.Run(rules, bars, new StepPlan(maxSteps), start);
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var year = years[day.Date.Year];
            // A candidate meets the candidate rules that day; a stock only held (building, holding) isn't one.
            if (day.Status is RuleStatus.Watching or RuleStatus.BuySignal or RuleStatus.SignalEnded) year.Candidates.Add(stock.Symbol);
            if (day.NewBuy && !(i == 0 && openStep is { } open && open == day.StepNo))
            {
                year.Signalled.Add(stock.Symbol);
                years[day.Date.Year] = year with { Signals = year.Signals + 1 };
            }
        }
    }

    /// <summary>
    /// The step of the buy signal open on the last of <paramref name="bars"/> (the trading day before a year starts), or null: that day alone,
    /// evaluated as fresh as the year's own first day, so it says only whether the window was open then.
    /// </summary>
    static int? OpenAtYearEnd(string templateKey, JsonDocument parameters, int maxSteps, IReadOnlyList<PriceBar> bars)
    {
        // Only the last day, fresh (no position, no day before): a run over more days would act on its own signal and read Building.
        var rules = TemplateRules.Create(templateKey, bars, parameters);
        var last = RulesRunner.Run(rules, bars, new StepPlan(maxSteps), bars.Count - 1)[^1];
        return last.Status == RuleStatus.BuySignal ? last.StepNo : null;
    }
}
