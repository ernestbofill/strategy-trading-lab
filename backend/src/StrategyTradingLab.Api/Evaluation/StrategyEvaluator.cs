using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Evaluation;

/// <summary>What the strategy's signals use of its budget so far.</summary>
public sealed class BudgetUse(decimal used)
{
    public decimal Used { get; set; } = used;
}

/// <summary>What one evaluation run did.</summary>
public record EvaluationSummary(int Strategies, int NewSignals, int NewTrades, int Missed, int Failed);

/// <summary>What one strategy or stock gained in a run.</summary>
record Changes(int NewSignals, int NewTrades, int Missed);

/// <summary>
/// The daily check (spec: How the two modes run): runs each running strategy's rules over each of its stocks' stored
/// prices, up to the last finished US session, and keeps what they say: a signal for each step the rules call for, its
/// signal trade at the next day's opening price once that day has one, and where the stock stands today. A strategy's
/// first check looks at the latest finished day only, and the rules start fresh from there, as the POC's backtest does on its start day. Idempotent: a second run the same day changes nothing.
/// </summary>
public class StrategyEvaluator(AppDbContext db, AdminAlerts alerts, TimeProvider time, ILogger<StrategyEvaluator> logger)
{
    /// <summary>
    /// A stock whose newest price is more sessions behind than this is missing data. Holidays aren't known, and other exchanges
    /// close on other days, so a day or two behind is normal.
    /// </summary>
    public const int StaleAfterSessions = 3;

    public async Task<EvaluationSummary> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var lastSession = UsSession.LastComplete(now);
        var strategies = await db.Strategies.AsNoTracking().Include(s => s.Assets)
            .Where(s => s.Status == StrategyStatus.Running).ToListAsync(cancellationToken);
        var rates = await ExchangeRates.LoadAsync(db, null, cancellationToken);
        // A run that loaded a strategy just before it ended can leave it a waiting signal: the next run closes it.
        var leftOver = await db.Signals.Where(s => s.Status == SignalStatus.Waiting
                && db.Strategies.Any(st => st.Id == s.StrategyId && st.Status == StrategyStatus.Ended))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Missed)
                .SetProperty(x => x.EndedOn, x => x.EndedOn ?? lastSession)
                .SetProperty(x => x.SnoozedUntil, (DateOnly?)null), cancellationToken);

        int signals = 0, trades = 0, missed = leftOver, failed = 0, evaluated = 0;
        // What kept a strategy from being checked in full, for the owner's Alerts page.
        var problems = new List<(Strategy Strategy, string Title, string Detail)>();
        foreach (var strategy in strategies.Where(s => TemplateRules.Supports(s.TemplateKey)))
        {
            var missing = new List<string>();
            try
            {
                // Locked while it is checked, with its list read under the lock: Edit list waits, so an unticked stock gets no new signal.
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM strategies WHERE id = {strategy.Id} FOR NO KEY UPDATE", cancellationToken);
                strategy.Assets = await db.StrategyAssets.AsNoTracking().Where(a => a.StrategyId == strategy.Id).ToListAsync(cancellationToken);
                var run = await EvaluateAsync(strategy, lastSession, now, rates, missing, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                signals += run.NewSignals;
                trades += run.NewTrades;
                missed += run.Missed;
                evaluated++;
                if (missing.Count > 0)
                    problems.Add((strategy, $"\"{strategy.Name}\" is missing price data", string.Join("\n", missing)));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                failed++;
                logger.LogError(error, "Evaluating strategy {Strategy} failed.", strategy.Id);
                problems.Add((strategy, $"\"{strategy.Name}\" couldn't be checked", error.Message));
            }
        }
        await alerts.SyncStrategiesAsync(AlertSource.Strategies, "strategy", problems, cancellationToken);
        return new EvaluationSummary(evaluated, signals, trades, missed, failed);
    }

    /// <param name="missing">Gets a line for each stock the rules couldn't check in full: no prices, prices too old, or too short a history.</param>
    async Task<Changes> EvaluateAsync(Strategy strategy, DateOnly lastSession, DateTimeOffset now, ExchangeRates rates, List<string> missing,
        CancellationToken cancellationToken)
    {
        int signals = 0, trades = 0, missed = await CloseUnfollowedAsync(strategy, lastSession, cancellationToken);
        var added = strategy.Assets.Where(a => a.Active && a.RemovedAt is null).ToDictionary(a => a.AssetId, a => DateOnly.FromDateTime(a.AddedAt.UtcDateTime));
        var wanted = added.Keys.ToList();
        // The budget the signals so far use (confirmed ones, and those still waiting: a skipped or missed one uses none); new signals stop once the next step wouldn't fit (DATA-MODEL: strategies).
        var budget = new BudgetUse(await db.Signals.Where(s => s.StrategyId == strategy.Id && (s.Status == SignalStatus.Confirmed || s.Status == SignalStatus.Waiting))
            .SumAsync(s => s.StepAmount ?? 0, cancellationToken));
        var assets = await db.Assets.AsNoTracking().Where(a => a.Active && wanted.Contains(a.Id)).ToListAsync(cancellationToken);
        foreach (var asset in assets)
        {
            // A stock added later is evaluated from the day it was added, not from the strategy's start.
            var start = added[asset.Id] > strategy.StartedOn ? added[asset.Id] : strategy.StartedOn;
            if (start > DateOnly.FromDateTime(now.UtcDateTime).AddDays(1)) continue; // Nothing if the strategy (or stock) starts in the future.

            var bars = await db.Prices.AsNoTracking().Where(p => p.AssetId == asset.Id && p.Date <= lastSession).OrderBy(p => p.Date)
                .Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToListAsync(cancellationToken);
            if (bars.Count == 0)
            {
                missing.Add($"{asset.Symbol}: no prices stored.");
                continue;
            }
            if (UsSession.WeekdaysBetween(bars[^1].Date, lastSession) > StaleAfterSessions)
                missing.Add($"{asset.Symbol}: the newest price is from {bars[^1].Date:yyyy-MM-dd}.");

            var (firstChecked, checkedUntil) = await CheckedDaysAsync(strategy.Id, asset.Id, cancellationToken);
            // A stock ticked again after its last check (Edit list: added after the start, yet checked before) gets no signal for
            // the days it was off the list: its first check back looks at the latest day only, as for a stock just added (no back-dating, #45).
            if (checkedUntil is { } until && added[asset.Id] > strategy.StartedOn && added[asset.Id] > until && bars[^1].Date > until)
                checkedUntil = bars[^1].Date.AddDays(-1);
            var replayFrom = firstChecked ?? bars[^1].Date;
            // No bar from that day on (a price row revised away): the latest bar, never back to the start.
            var replayIndex = bars.FindIndex(b => b.Date >= replayFrom);
            var from = replayIndex < 0 ? bars.Count - 1 : replayIndex;

            var rules = TemplateRules.Create(strategy.TemplateKey, bars, strategy.Params);
            var days = RulesRunner.Run(rules, bars, new StepPlan(strategy.Budget.MaxSteps ?? 1), from);
            if (days[^1].Status == RuleStatus.NotEnoughData)
                missing.Add($"{asset.Symbol}: not enough price history for the rules ({bars.Count} days stored).");
            var run = await SyncAsync(strategy, asset, bars, days, checkedUntil, now, rates, budget, cancellationToken);
            signals += run.NewSignals;
            trades += run.NewTrades;
            missed += run.Missed;
        }
        return new Changes(signals, trades, missed);
    }

    /// <summary>
    /// The waiting signals of stocks the daily check no longer follows (delisted, unticked or removed from the strategy) would
    /// never close: they become missed, ending on the last session. Returns how many.
    /// </summary>
    async Task<int> CloseUnfollowedAsync(Strategy strategy, DateOnly lastSession, CancellationToken cancellationToken)
    {
        var ticked = strategy.Assets.Where(a => a.Active && a.RemovedAt is null).Select(a => a.AssetId).ToList();
        // Read in the database, so an answer saved meanwhile stands. Only a waiting signal can be snoozed (the table's check).
        return await db.Signals.Where(s => s.StrategyId == strategy.Id && s.Status == SignalStatus.Waiting
                && !db.Assets.Any(a => a.Id == s.AssetId && a.Active && ticked.Contains(a.Id)))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Missed)
                .SetProperty(x => x.EndedOn, x => x.EndedOn ?? lastSession)
                .SetProperty(x => x.SnoozedUntil, (DateOnly?)null), cancellationToken);
    }

    /// <summary>
    /// The first day the rules replay for this strategy and stock, and the last day checked before (ROADMAP item 7, decided in #58).
    /// The first run evaluates the latest finished day only, as for a stock just added, so a strategy that existed before its
    /// engine ran gets no backlog of old signals (the first day is null then). Later runs replay from that first day, or from an earlier
    /// signal's day (a strategy that got a backlog before this rule keeps counting those steps). Days checked before the price
    /// history was long enough don't count: the rules couldn't run, so once the history is there the stock starts fresh and the
    /// latest day checked can still get its signal (none before it). After the first real check or the first signal every day counts, so a later short
    /// history never opens the past to new signals or restarts the steps.
    /// </summary>
    async Task<(DateOnly? First, DateOnly? Until)> CheckedDaysAsync(Guid strategyId, Guid assetId, CancellationToken cancellationToken)
    {
        var days = await db.DailyEvaluations.Where(r => r.StrategyId == strategyId && r.AssetId == assetId)
            .GroupBy(r => 1)
            .Select(g => new { First = g.Where(r => r.Status != EvaluationStatus.NotEnoughData).Min(r => (DateOnly?)r.Date), Until = g.Max(r => (DateOnly?)r.Date) })
            .SingleOrDefaultAsync(cancellationToken);
        var firstSignal = await db.Signals.Where(s => s.StrategyId == strategyId && s.AssetId == assetId)
            .MinAsync(s => (DateOnly?)s.SignalDate, cancellationToken);
        DateOnly? first = (days?.First, firstSignal) switch
        {
            ({ } check, { } signal) => check < signal ? check : signal,
            (var check, var signal) => check ?? signal,
        };
        // Only short-of-history days so far: a first run, but no day before the last one checked gets a signal.
        return first is null ? (null, days?.Until?.AddDays(-1)) : (first, days?.Until);
    }

    /// <param name="evaluatedUntil">The last day checked before (<see cref="CheckedDaysAsync"/>).</param>
    async Task<Changes> SyncAsync(Strategy strategy, Asset asset, IReadOnlyList<PriceBar> bars, IReadOnlyList<DayEvaluation> days,
        DateOnly? evaluatedUntil, DateTimeOffset now, ExchangeRates rates, BudgetUse budget, CancellationToken cancellationToken)
    {
        int newSignals = 0, newTrades = 0, missed = 0;

        // Where it stands today.
        var latest = days[^1];
        var status = Enum.Parse<EvaluationStatus>(latest.Status.ToString());
        var row = await db.DailyEvaluations.FindAsync([strategy.Id, asset.Id, latest.Date], cancellationToken);
        if (row is null)
            db.DailyEvaluations.Add(new DailyEvaluation
                { StrategyId = strategy.Id, AssetId = asset.Id, Date = latest.Date, Status = status, StepNo = latest.StepNo, Facts = TodaysFacts(latest) });
        else
        {
            row.Status = status;
            row.StepNo = latest.StepNo;
            row.Facts = TodaysFacts(latest);
        }

        // A signal for each buy or sale the rules call for. Days up to the last one evaluated before are never given new
        // signals: a changed setting or a revised price history must not back-date signals and trades.
        var signals = await db.Signals.Where(s => s.StrategyId == strategy.Id && s.AssetId == asset.Id)
            .ToDictionaryAsync(s => (s.SignalDate, s.Side), cancellationToken);
        var live = strategy.Mode == StrategyMode.Live;
        var hasSteps = strategy.Budget.Step is not null && strategy.Budget.MaxSteps is not null;
        foreach (var day in days.Where(d => d.NewBuy && !signals.ContainsKey((d.Date, TradeSide.Buy)) && (evaluatedUntil is null || d.Date > evaluatedUntil)))
        {
            var stepAmount = strategy.Budget.Step ?? 0;
            if (budget.Used + stepAmount > strategy.Budget.Amount) continue; // The budget is used up: no more signals.
            budget.Used += stepAmount;
            var signal = new Signal
            {
                StrategyId = strategy.Id, AssetId = asset.Id, Side = TradeSide.Buy,
                StepNo = hasSteps ? day.StepNo : null, MaxSteps = hasSteps ? strategy.Budget.MaxSteps : null,
                StepAmount = hasSteps ? strategy.Budget.Step : null,
                SignalDate = day.Date, ClosePrice = bars.First(b => b.Date == day.Date).Close,
                Facts = Facts(day.Facts), Status = live ? SignalStatus.Waiting : SignalStatus.Confirmed, AnsweredAt = live ? null : now,
            };
            db.Signals.Add(signal);
            signals[(day.Date, TradeSide.Buy)] = signal;
            newSignals++;
        }
        foreach (var day in days.Where(d => d.NewSell && !signals.ContainsKey((d.Date, TradeSide.Sell)) && (evaluatedUntil is null || d.Date > evaluatedUntil)))
        {
            var signal = new Signal
            {
                StrategyId = strategy.Id, AssetId = asset.Id, Side = TradeSide.Sell,
                StepNo = null, MaxSteps = null, StepAmount = null,
                SignalDate = day.Date, ClosePrice = bars.First(b => b.Date == day.Date).Close,
                Facts = Facts(day.Facts), Status = live ? SignalStatus.Waiting : SignalStatus.Confirmed, AnsweredAt = live ? null : now,
            };
            db.Signals.Add(signal);
            signals[(day.Date, TradeSide.Sell)] = signal;
            newSignals++;
        }

        // Where a signal's window closed: the first later day the buy (or sell) conditions no longer hold. A signal still waiting is missed.
        foreach (var signal in signals.Values.Where(s => s.EndedOn is null))
        {
            var closed = days.FirstOrDefault(d => d.Date > signal.SignalDate
                && (signal.Side == TradeSide.Sell ? d.WindowOpen : !d.WindowOpen));
            if (closed is null) continue;
            signal.EndedOn = closed.Date;
        }
        await db.SaveChangesAsync(cancellationToken);
        // Every ended signal still waiting in the database is missed: not only today's, so a run stopped between the two
        // writes is repaired by the next. Read in the database, so an answer saved since these rows were read stands.
        missed += await db.Signals.Where(s => s.StrategyId == strategy.Id && s.AssetId == asset.Id && s.EndedOn != null && s.Status == SignalStatus.Waiting)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Missed)
                    .SetProperty(x => x.SnoozedUntil, (DateOnly?)null), cancellationToken); // Only a waiting signal can be snoozed (the table's check).

        // Each signal's own trade, at the next trading day's open, once that day exists.
        var existingTrades = await db.SignalTrades.AsNoTracking()
            .Where(t => t.StrategyId == strategy.Id && t.AssetId == asset.Id)
            .OrderBy(t => t.TradeDate).ThenBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
        var traded = existingTrades.Select(t => t.SignalId).ToHashSet();
        decimal heldShares = 0, cashToInvest = strategy.Budget.Amount;
        foreach (var existing in existingTrades)
        {
            if (existing.Side == TradeSide.Buy) heldShares += existing.Shares;
            else { heldShares = Math.Max(0, heldShares - existing.Shares); cashToInvest = Math.Max(0, existing.Amount - existing.Fee); }
        }
        foreach (var signal in signals.Values.Where(s => !traded.Contains(s.Id)).OrderBy(s => s.SignalDate).ThenBy(s => s.Side == TradeSide.Buy ? 0 : 1))
        {
            var next = bars.FirstOrDefault(b => b.Date > signal.SignalDate);
            if (next is null || rates.Between(strategy.Currency, asset.Currency, next.Date) is not { } fx) continue;
            if (signal.Side == TradeSide.Buy)
            {
                // The step (or for all-in, the budget / sale proceeds) includes its fee: amount plus fee is the step.
                if (BuyTrade(signal, strategy, next.Date, next.Open, fx, signal.StepAmount ?? cashToInvest) is not { } trade) continue;
                db.SignalTrades.Add(trade);
                heldShares += trade.Shares;
                newTrades++;
            }
            else if (heldShares > 0)
            {
                var amount = Math.Round(heldShares * next.Open / fx, 2);
                if (amount <= 0) continue;
                var fee = Math.Round(amount * (TemplateCatalog.Fee.Percent / 100) + TemplateCatalog.Fee.Fixed, 2);
                db.SignalTrades.Add(new SignalTrade
                {
                    SignalId = signal.Id, StrategyId = strategy.Id, AssetId = asset.Id, TradeDate = next.Date, Side = TradeSide.Sell, StepNo = null,
                    Price = next.Open, Shares = heldShares, Amount = amount, Fee = fee, FxRate = fx,
                });
                cashToInvest = Math.Max(0, amount - fee);
                heldShares = 0;
                newTrades++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new Changes(newSignals, newTrades, missed);
    }

    /// <summary>
    /// A signal's buy trade at an opening price: <paramref name="spend"/> (the step, or what an all-in buy reinvests) includes its
    /// fee, so amount plus fee is the step. Null when it leaves nothing to buy with.
    /// </summary>
    public static SignalTrade? BuyTrade(Signal signal, Strategy strategy, DateOnly date, decimal open, decimal fx, decimal spend)
    {
        if (spend <= TemplateCatalog.Fee.Fixed + 0.01m) return null;
        var (amount, fee) = TemplateCatalog.Fee.Split(spend);
        var shares = Math.Round(amount * fx / open, 6);
        if (shares <= 0) return null;
        return new SignalTrade
        {
            SignalId = signal.Id, StrategyId = strategy.Id, AssetId = signal.AssetId, TradeDate = date, Side = TradeSide.Buy, StepNo = signal.StepNo,
            Price = open, Shares = shares, Amount = amount, Fee = fee, FxRate = fx,
        };
    }

    static JsonDocument Facts(IReadOnlyDictionary<string, object> facts) => JsonSerializer.SerializeToDocument(facts, JsonSerializerOptions.Web);

    /// <summary>Where the stock stands today: the day's facts and, under <c>checks</c>, each rule's verdict (the stock page).</summary>
    static JsonDocument TodaysFacts(DayEvaluation day) =>
        JsonSerializer.SerializeToDocument(new Dictionary<string, object>(day.Facts) { [ChecksKey] = day.Checks }, JsonSerializerOptions.Web);

    /// <summary>The key of the checks in a daily evaluation's facts.</summary>
    public const string ChecksKey = "checks";
}

/// <summary>The euro against each currency by day, and the rate between any two through it.</summary>
public class ExchangeRates(Dictionary<string, List<(DateOnly Date, decimal Rate)>> byQuote)
{
    public const string Base = "EUR";

    /// <summary>How far before a range prices and rates are loaded, so its first days still find the last trading day before them.</summary>
    public const int LookBackDays = 7;

    /// <summary>The stored rates from <see cref="LookBackDays"/> before <paramref name="from"/> on (all of them when null): a rate older than that isn't used.</summary>
    public static async Task<ExchangeRates> LoadAsync(AppDbContext db, DateOnly? from, CancellationToken cancellationToken)
    {
        var since = from?.AddDays(-LookBackDays) ?? DateOnly.MinValue;
        var rows = await db.FxRates.AsNoTracking().Where(r => r.Base == Base && r.Date >= since).OrderBy(r => r.Date)
            .Select(r => new { r.Quote, r.Date, r.Rate }).ToListAsync(cancellationToken);
        return new ExchangeRates(rows.GroupBy(r => r.Quote).ToDictionary(g => g.Key, g => g.Select(r => (r.Date, r.Rate)).ToList()));
    }

    /// <summary>Units of <paramref name="quote"/> per euro on the latest day on or before <paramref name="date"/>; null when unknown.</summary>
    decimal? PerEuro(string currency, DateOnly date)
    {
        if (currency == Base) return 1;
        if (!byQuote.TryGetValue(currency, out var days)) return null;
        var at = days.FindLastIndex(d => d.Date <= date);
        return at < 0 ? null : days[at].Rate;
    }

    /// <summary>Whether <paramref name="currency"/> has a rate stored for that very day (the euro always has).</summary>
    public bool StoredOn(string currency, DateOnly date) =>
        currency == Base || (byQuote.TryGetValue(currency, out var days) && days.Exists(d => d.Date == date));

    /// <summary>Units of <paramref name="assetCurrency"/> per one unit of <paramref name="strategyCurrency"/> on a day (USD per EUR, a trade's fx rate).</summary>
    public decimal? Between(string strategyCurrency, string assetCurrency, DateOnly date)
    {
        if (strategyCurrency == assetCurrency) return 1;
        return PerEuro(assetCurrency, date) is { } asset && PerEuro(strategyCurrency, date) is { } strategy ? asset / strategy : null;
    }
}
