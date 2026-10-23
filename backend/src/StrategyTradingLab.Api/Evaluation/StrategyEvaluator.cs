using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Evaluation;

/// <summary>What one evaluation run did.</summary>
public record EvaluationSummary(int Strategies, int NewSignals, int NewTrades, int Missed, int Failed);

/// <summary>
/// The daily check (spec: How the two modes run): runs each running strategy's rules over each of its stocks' stored
/// prices, up to the last finished US session, and keeps what they say: a signal for each step the rules call for, its
/// signal trade at the next day's opening price once that day has one, and where the stock stands today. A strategy
/// starts fresh on its start day, as the POC's backtest does. Idempotent: a second run the same day changes nothing.
/// </summary>
public class StrategyEvaluator(AppDbContext db, TimeProvider time, ILogger<StrategyEvaluator> logger)
{
    public async Task<EvaluationSummary> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var lastSession = UsSession.LastComplete(now);
        var strategies = await db.Strategies.AsNoTracking().Include(s => s.Assets)
            .Where(s => s.Status == StrategyStatus.Running).ToListAsync(cancellationToken);
        var rates = await LoadRatesAsync(cancellationToken);

        int signals = 0, trades = 0, missed = 0, failed = 0, evaluated = 0;
        foreach (var strategy in strategies.Where(s => TemplateRules.Supports(s.TemplateKey)))
        {
            try
            {
                var run = await EvaluateAsync(strategy, lastSession, now, rates, cancellationToken);
                signals += run.NewSignals;
                trades += run.NewTrades;
                missed += run.Missed;
                evaluated++;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                failed++;
                logger.LogError(error, "Evaluating strategy {Strategy} failed.", strategy.Id);
            }
        }
        return new EvaluationSummary(evaluated, signals, trades, missed, failed);
    }

    async Task<EvaluationSummary> EvaluateAsync(Strategy strategy, DateOnly lastSession, DateTimeOffset now, ExchangeRates rates,
        CancellationToken cancellationToken)
    {
        int signals = 0, trades = 0, missed = 0;
        var wanted = strategy.Assets.Where(a => a.Active && a.RemovedAt is null).Select(a => a.AssetId).ToList();
        var assets = await db.Assets.AsNoTracking().Where(a => a.Active && wanted.Contains(a.Id)).ToListAsync(cancellationToken);
        foreach (var asset in assets)
        {
            var bars = await db.Prices.AsNoTracking().Where(p => p.AssetId == asset.Id && p.Date <= lastSession).OrderBy(p => p.Date)
                .Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToListAsync(cancellationToken);
            var from = bars.FindIndex(b => b.Date >= strategy.StartedOn);
            if (from < 0) continue; // Nothing since the strategy started.

            var rules = TemplateRules.Create(strategy.TemplateKey, bars, strategy.Params);
            var days = RulesRunner.Run(rules, bars, new StepPlan(strategy.Budget.MaxSteps ?? 1), from);
            var run = await SyncAsync(strategy, asset, bars, days, now, rates, cancellationToken);
            signals += run.NewSignals;
            trades += run.NewTrades;
            missed += run.Missed;
        }
        return new EvaluationSummary(1, signals, trades, missed, 0);
    }

    async Task<EvaluationSummary> SyncAsync(Strategy strategy, Asset asset, IReadOnlyList<PriceBar> bars, IReadOnlyList<DayEvaluation> days,
        DateTimeOffset now, ExchangeRates rates, CancellationToken cancellationToken)
    {
        int newSignals = 0, newTrades = 0, missed = 0;

        // Where it stands today.
        var latest = days[^1];
        var status = Enum.Parse<EvaluationStatus>(latest.Status.ToString());
        var row = await db.DailyEvaluations.FindAsync([strategy.Id, asset.Id, latest.Date], cancellationToken);
        if (row is null)
            db.DailyEvaluations.Add(new DailyEvaluation
                { StrategyId = strategy.Id, AssetId = asset.Id, Date = latest.Date, Status = status, StepNo = latest.StepNo, Facts = Facts(latest.Facts) });
        else
        {
            row.Status = status;
            row.StepNo = latest.StepNo;
            row.Facts = Facts(latest.Facts);
        }

        // A signal for each step the rules call for.
        var signals = await db.Signals.Where(s => s.StrategyId == strategy.Id && s.AssetId == asset.Id && s.Side == TradeSide.Buy)
            .ToDictionaryAsync(s => s.SignalDate, cancellationToken);
        foreach (var day in days.Where(d => d.NewBuy && !signals.ContainsKey(d.Date)))
        {
            var live = strategy.Mode == StrategyMode.Live;
            var signal = new Signal
            {
                StrategyId = strategy.Id, AssetId = asset.Id, Side = TradeSide.Buy, StepNo = day.StepNo, MaxSteps = strategy.Budget.MaxSteps,
                StepAmount = strategy.Budget.Step, SignalDate = day.Date, ClosePrice = bars.First(b => b.Date == day.Date).Close,
                Facts = Facts(day.Facts), Status = live ? SignalStatus.Waiting : SignalStatus.Confirmed, AnsweredAt = live ? null : now,
            };
            db.Signals.Add(signal);
            signals[day.Date] = signal;
            newSignals++;
        }

        // Where a signal's window closed: the first later day the buy conditions no longer hold. A signal still waiting is missed.
        foreach (var signal in signals.Values.Where(s => s.EndedOn is null))
        {
            var closed = days.FirstOrDefault(d => d.Date > signal.SignalDate && !d.WindowOpen);
            if (closed is null) continue;
            signal.EndedOn = closed.Date;
            if (signal.Status == SignalStatus.Waiting)
            {
                signal.Status = SignalStatus.Missed;
                missed++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        // Each signal's own trade, at the next trading day's open, once that day exists.
        var traded = await db.SignalTrades.Where(t => t.StrategyId == strategy.Id && t.AssetId == asset.Id).Select(t => t.SignalId).ToListAsync(cancellationToken);
        foreach (var signal in signals.Values.Where(s => !traded.Contains(s.Id) && s.StepAmount is not null))
        {
            var next = bars.FirstOrDefault(b => b.Date > signal.SignalDate);
            if (next is null || rates.Between(strategy.Currency, asset.Currency, next.Date) is not { } fx) continue;
            var amount = signal.StepAmount!.Value;
            var fee = Math.Round(amount * TemplateCatalog.Fee.Percent / 100 + TemplateCatalog.Fee.Fixed, 2);
            db.SignalTrades.Add(new SignalTrade
            {
                SignalId = signal.Id, StrategyId = strategy.Id, AssetId = asset.Id, TradeDate = next.Date, Side = TradeSide.Buy, StepNo = signal.StepNo,
                Price = next.Open, Shares = Math.Round(amount * fx / next.Open, 6), Amount = amount, Fee = fee, FxRate = fx,
            });
            newTrades++;
        }
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new EvaluationSummary(0, newSignals, newTrades, missed, 0);
    }

    static JsonDocument Facts(IReadOnlyDictionary<string, object> facts) => JsonSerializer.SerializeToDocument(facts, JsonSerializerOptions.Web);

    async Task<ExchangeRates> LoadRatesAsync(CancellationToken cancellationToken)
    {
        var rows = await db.FxRates.AsNoTracking().Where(r => r.Base == ExchangeRates.Base).OrderBy(r => r.Date)
            .Select(r => new { r.Quote, r.Date, r.Rate }).ToListAsync(cancellationToken);
        return new ExchangeRates(rows.GroupBy(r => r.Quote).ToDictionary(g => g.Key, g => g.Select(r => (r.Date, r.Rate)).ToList()));
    }
}

/// <summary>The euro against each currency by day, and the rate between any two through it.</summary>
public class ExchangeRates(Dictionary<string, List<(DateOnly Date, decimal Rate)>> byQuote)
{
    public const string Base = "EUR";

    /// <summary>Units of <paramref name="quote"/> per euro on the latest day on or before <paramref name="date"/>; null when unknown.</summary>
    decimal? PerEuro(string currency, DateOnly date)
    {
        if (currency == Base) return 1;
        if (!byQuote.TryGetValue(currency, out var days)) return null;
        var at = days.FindLastIndex(d => d.Date <= date);
        return at < 0 ? null : days[at].Rate;
    }

    /// <summary>Units of <paramref name="assetCurrency"/> per one unit of <paramref name="strategyCurrency"/> on a day (USD per EUR, a trade's fx rate).</summary>
    public decimal? Between(string strategyCurrency, string assetCurrency, DateOnly date)
    {
        if (strategyCurrency == assetCurrency) return 1;
        return PerEuro(assetCurrency, date) is { } asset && PerEuro(strategyCurrency, date) is { } strategy ? asset / strategy : null;
    }
}
