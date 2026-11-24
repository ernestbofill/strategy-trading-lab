using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Common;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Notifications;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Signals;

/// <summary>
/// Confirm a signal (spec: Confirm a signal), live strategies only: the signal with what the form starts with, and its three
/// answers: the trade the user made, a skip, or a snooze until the next weekday. A waiting or missed signal can be answered
/// (later, with the real date) until its stock moves on; an answered one can't be answered again.
/// </summary>
public static class SignalEndpoints
{
    /// <summary>The unique index on user_trades (signal_id) WHERE deleted_at IS NULL: one trade per signal, also when two saves race.</summary>
    const string OneTradeIndex = "ix_user_trades_signal_id";

    public static void MapSignals(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/signals").WithTags("Signals").RequireUser();

        group.MapGet("/{id:guid}", async Task<Results<Ok<SignalDetail>, NotFound>> (
            Guid id, AppDbContext db, Sessions sessions, PriceRefresher prices, PriceRefreshState refreshes,
            Microsoft.Extensions.Options.IOptions<PriceRefreshOptions> options, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await sessions.RequiredAsync(cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            var next = await NextOpenAsync(db, found.Signal, cancellationToken);
            var latest = next is null && options.Value.FetchOnDemand && Answerable(found.Signal)
                ? await LatestPriceAsync(prices, refreshes, found, time, cancellationToken) : null;
            return TypedResults.Ok(await DetailAsync(db, found, user, UserClock.Today(user, time), next, latest, cancellationToken));
        })
        .WithName("GetSignal")
        .WithSummary("A live signal, with what Confirm a signal's form starts with.");

        group.MapPost("/{id:guid}/trade", async Task<Results<NoContent, ValidationProblem, NotFound, Conflict<string>>> (
            Guid id, SignalTradeRequest request, AppDbContext db, Sessions sessions, PriceRefreshState refreshes, TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var user = await sessions.RequiredAsync(cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            var (signal, strategy, asset) = found;
            if (!Answerable(signal)) return TypedResults.Conflict(Messages.AlreadyAnswered.In(user.Locale));
            if (await MovedOnAsync(db, signal, cancellationToken)) return TypedResults.Conflict(Messages.SignalMovedOn.In(user.Locale));
            // An ended strategy's history is final: its missed signals stay missed.
            if (strategy.Status != StrategyStatus.Running) return TypedResults.Conflict(Messages.StrategyIsEnded.In(user.Locale));

            // The same currency needs no rate (it is 1): nothing to load.
            var rates = await ExchangeRates.ForPairAsync(db, strategy.Currency, asset.Currency, signal.SignalDate, cancellationToken);
            var (trade, errors) = SignalRules.CheckTrade(request, signal.SignalDate, UserClock.Today(user, time),
                day => rates.Between(strategy.Currency, asset.Currency, day), locale: user.Locale);
            if (trade is null) return TypedResults.ValidationProblem(errors);

            var userTrade = new UserTrade
            {
                StrategyId = strategy.Id, AssetId = asset.Id, TradeDate = trade.Date, Side = signal.Side, Price = trade.Price, EnteredAs = trade.EnteredAs,
                Amount = trade.Amount, Shares = trade.Shares, Fee = trade.Fee, FxRate = trade.FxRate, FxSource = trade.FxSource,
                Source = TradeSource.Signal, SignalId = signal.Id,
            };
            if (!await AnswerAsync(db, signal.Id, SignalStatus.Confirmed, null, userTrade, time, cancellationToken))
                return TypedResults.Conflict(Messages.AlreadyAnswered.In(user.Locale));
            // The step is bought now: the strategy's rules run again, so the stock reads as the user holds it before the next daily run.
            refreshes.RequestCheck(strategy.Id);
            return TypedResults.NoContent();
        })
        .WithName("ConfirmSignal")
        .WithSummary("\"I did it\": records the user's trade for the signal.");

        group.MapPost("/{id:guid}/skip", async Task<Results<NoContent, NotFound, Conflict<string>>> (
            Guid id, SignalSkipRequest request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await sessions.RequiredAsync(cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            if (!Answerable(found.Signal)) return TypedResults.Conflict(Messages.AlreadyAnswered.In(user.Locale));
            if (await MovedOnAsync(db, found.Signal, cancellationToken)) return TypedResults.Conflict(Messages.SignalMovedOn.In(user.Locale));
            if (found.Strategy.Status != StrategyStatus.Running) return TypedResults.Conflict(Messages.StrategyIsEnded.In(user.Locale));
            return await AnswerAsync(db, id, SignalStatus.Skipped, request.Reason, null, time, cancellationToken)
                ? TypedResults.NoContent() : TypedResults.Conflict(Messages.AlreadyAnswered.In(user.Locale));
        })
        .WithName("SkipSignal")
        .WithSummary("\"I skipped it\": uses no budget and buys no step.");

        group.MapPost("/{id:guid}/snooze", async Task<Results<Ok<SnoozeResult>, NotFound, Conflict<string>>> (
            Guid id, AppDbContext db, Sessions sessions, TimeProvider time, IPushSender push, CancellationToken cancellationToken) =>
        {
            var user = await sessions.RequiredAsync(cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            // Only a waiting signal can be snoozed (the table's check): a missed one has nothing left to remind of.
            if (found.Signal.Status != SignalStatus.Waiting) return TypedResults.Conflict(Messages.OnlyWaitingCanSnooze.In(user.Locale));
            // AI round signals cannot be snoozed: they remain due until they expire.
            if (found.Strategy.TemplateKey == TemplateKeys.Ai) return TypedResults.Conflict(Messages.AiSignalsCannotSnooze.In(user.Locale));
            // A snooze promises a reminder: refuse it when none could be sent.
            if (found.Strategy.Status != StrategyStatus.Running) return TypedResults.Conflict(Messages.StrategyEnded.In(user.Locale));
            // By email (switched on and confirmed) or by push (switched on, sent by this server, with a device that gets it).
            var byEmail = user.AlertEmails && user.EmailVerifiedAt is not null;
            var byPush = user.AlertPush && push.PublicKey is not null && await db.PushSubscriptions.AnyAsync(p => p.UserId == user.Id, cancellationToken);
            if (!byEmail && !byPush)
                return TypedResults.Conflict((user.AlertEmails ? Messages.ConfirmEmailForReminders
                    // Without push on this server, the device isn't a way to offer.
                    : push.PublicKey is null ? Messages.ReminderEmailsOff : Messages.NotificationsOff).In(user.Locale));
            // The next US market day (spec: a day is a US market day), counted from the later of New York's date and the user's,
            // so it is always after the user's own today: the reminder goes out from 07:00 of that day in their time zone.
            var newYork = UsSession.Today(time.GetUtcNow());
            var local = UserClock.Today(user, time);
            var until = SignalRules.NextWeekday(local > newYork ? local : newYork);
            // Conditional, like the answers: the signal may have been answered or missed since it was read.
            var snoozed = await db.Signals.Where(s => s.Id == id && s.Status == SignalStatus.Waiting)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SnoozedUntil, until), cancellationToken);
            return snoozed == 1 ? TypedResults.Ok(new SnoozeResult(until)) : TypedResults.Conflict(Messages.OnlyWaitingCanSnooze.In(user.Locale));
        })
        .WithName("SnoozeSignal")
        .WithSummary("Snooze: the signal stays waiting and the user is reminded the next weekday.");
    }

    /// <summary>A signal of one of the user's live strategies; null for anything else (also another user's). Read only: the answers update it conditionally.</summary>
    static async Task<(Signal Signal, Strategy Strategy, Asset Asset)?> FindAsync(AppDbContext db, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var row = await (from signal in db.Signals.AsNoTracking()
                         join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                         join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                         where signal.Id == id && strategy.UserId == userId && strategy.Mode == StrategyMode.Live
                         select new { signal, strategy, asset }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.signal, row.strategy, row.asset);
    }

    static bool Answerable(Signal signal) => signal.Status is SignalStatus.Waiting or SignalStatus.Missed;

    /// <summary>
    /// The signals among <paramref name="signals"/> whose stock has moved on since they were missed: a check of a later day than the
    /// one they ended on, or a newer signal of their side. A late answer is for right after the miss (Ernest, 2026-11-24): after
    /// that a skipped or missed step can be signalled again, and answering the old signal could count it twice.
    /// </summary>
    static IQueryable<Signal> MovedOn(this IQueryable<Signal> signals, AppDbContext db) =>
        signals.Where(s => s.Status == SignalStatus.Missed
            && (db.DailyEvaluations.Any(e => e.StrategyId == s.StrategyId && e.AssetId == s.AssetId && e.Date > s.EndedOn)
                || db.Signals.Any(n => n.StrategyId == s.StrategyId && n.AssetId == s.AssetId && n.Side == s.Side && n.SignalDate > s.SignalDate)));

    /// <summary>Whether a missed signal's stock has moved on (<see cref="MovedOn(IQueryable{Signal}, AppDbContext)"/>).</summary>
    static Task<bool> MovedOnAsync(AppDbContext db, Signal signal, CancellationToken cancellationToken) =>
        signal.Status != SignalStatus.Missed ? Task.FromResult(false)
        : db.Signals.Where(s => s.Id == signal.Id).MovedOn(db).AnyAsync(cancellationToken);

    /// <summary>How long the page waits for the latest price from Yahoo, and how often an asset is asked for it.</summary>
    static readonly TimeSpan LatestTimeout = TimeSpan.FromSeconds(5), LatestEvery = TimeSpan.FromMinutes(5);

    /// <summary>The first stored trading day after the signal, with its open; null until the daily refresh stores it.</summary>
    static async Task<(DateOnly Date, decimal Open)?> NextOpenAsync(AppDbContext db, Signal signal, CancellationToken cancellationToken)
    {
        var next = await db.Prices.AsNoTracking().Where(p => p.AssetId == signal.AssetId && p.Date > signal.SignalDate).OrderBy(p => p.Date)
            .Select(p => new { p.Date, p.Open }).FirstOrDefaultAsync(cancellationToken);
        return next is null ? null : (next.Date, next.Open);
    }

    /// <summary>
    /// The day after the signal has no stored price yet (the daily refresh runs after the close) but its session has opened: the
    /// latest price, fetched and not stored (#220), so the form starts with a price the user could trade at. Best effort: a failure
    /// or a slow source gives null, and the form starts with the close.
    /// </summary>
    static async Task<LatestPrice?> LatestPriceAsync(PriceRefresher prices, PriceRefreshState refreshes,
        (Signal Signal, Strategy Strategy, Asset Asset) found, TimeProvider time, CancellationToken cancellationToken)
    {
        var (signal, _, asset) = found;
        var now = time.GetUtcNow();
        // Before the next session opens the latest price is still the signal day's close.
        if (now < UsSession.OpenAt(UsSession.After(signal.SignalDate))) return null;
        var latest = await refreshes.LatestPriceAsync(asset.Id, now, LatestEvery,
            () => prices.TryLatestAsync(asset.Symbol, cancellationToken, LatestTimeout), cancellationToken);
        // Not the signal day's close (before its exchange's next session opened, or a holiday there). The form dates it by its own
        // day, so a price from a later session (after a US holiday, or for a stock whose prices are no longer fetched) says so.
        return latest?.Date > signal.SignalDate ? latest : null;
    }

    /// <summary>
    /// Answers the signal and stores its trade, if any, in one transaction; false when it was already answered. The status
    /// changes only where it is still waiting or missed in the database, and that update locks the row, so of two answers at
    /// once (a phone and a computer, or the daily check marking it missed) only one wins and none is overwritten.
    /// </summary>
    static async Task<bool> AnswerAsync(AppDbContext db, Guid signalId, SignalStatus status, SkipReason? reason, UserTrade? trade,
        TimeProvider time, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear(); // Only this method writes: nothing loaded to check the request may be saved by it.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Hold the strategy row until this answer commits: End (which updates that row) waits for it, or this waits for End
        // and then sees the strategy ended.
        var running = await db.Database.SqlQuery<string>($"""
            SELECT st.status AS "Value" FROM strategies st JOIN signals s ON s.strategy_id = st.id
            WHERE s.id = {signalId} FOR SHARE OF st
            """).ToListAsync(cancellationToken);
        if (running is not ["running"]) return false;
        var now = time.GetUtcNow();
        // Nor a missed signal whose stock moved on since the request's check (the daily check writes under the strategy's lock).
        var movedOn = db.Signals.Where(s => s.Id == signalId).MovedOn(db);
        var answered = await db.Signals
            .Where(s => s.Id == signalId && (s.Status == SignalStatus.Waiting || s.Status == SignalStatus.Missed)
                // Checked again in the update: a strategy ended since the request's check takes no answer.
                && db.Strategies.Any(st => st.Id == s.StrategyId && st.Status == StrategyStatus.Running)
                && !movedOn.Any())
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.SkipReason, reason)
                .SetProperty(x => x.AnsweredAt, now)
                .SetProperty(x => x.SnoozedUntil, (DateOnly?)null), cancellationToken); // Only a waiting signal can be snoozed (the table's check).
        if (answered == 0) return false;
        if (trade is not null)
        {
            db.UserTrades.Add(trade);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            // The signal's trade already exists (its unique index): answered by another save after all.
            catch (DbUpdateException error) when (error.IsUniqueViolation(OneTradeIndex))
            {
                return false;
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <param name="next">The next open once stored (<see cref="NextOpenAsync"/>).</param>
    /// <param name="latest">The latest price when the next open isn't stored yet (<see cref="LatestPriceAsync"/>).</param>
    static async Task<SignalDetail> DetailAsync(AppDbContext db, (Signal Signal, Strategy Strategy, Asset Asset) found, User user, DateOnly today,
        (DateOnly Date, decimal Open)? next, LatestPrice? latest, CancellationToken cancellationToken)
    {
        var (signal, strategy, asset) = found;
        var texts = TemplateTexts.For(TemplateCatalog.Find(strategy.TemplateKey)!, user.Locale);

        // The trade day: the first trading day after the signal once its price is stored, else the latest price's day, else the next weekday.
        var nextDay = next?.Date ?? latest?.Date ?? SignalRules.NextWeekday(signal.SignalDate);
        decimal? prefillAmount = null, prefillShares = null;
        if (signal.Side == TradeSide.Buy)
        {
            var step = signal.StepAmount ?? strategy.Budget.Amount;
            if (TemplateCatalog.Fee.CanBuyWith(step)) prefillAmount = TemplateCatalog.Fee.Split(step).Amount;
        }
        else
        {
            var held = await db.UserTrades.AsNoTracking()
                .Where(t => t.StrategyId == strategy.Id && t.AssetId == asset.Id && t.DeletedAt == null)
                .SumAsync(t => t.Side == TradeSide.Buy ? t.Shares : -t.Shares, cancellationToken);
            if (held > 0) prefillShares = held;
        }
        // The next open once it is stored; before that the latest price once that session has opened; else the signal day's close.
        // Each is said as such.
        var prefill = new TradePrefill(nextDay > today ? today : nextDay, next?.Open ?? latest?.Price ?? signal.ClosePrice, prefillAmount, prefillShares,
            PriceIsClose: next is null && latest is null, PriceIsLatest: next is null && latest is not null);

        // From the look-back before the signal, as the server's own lookup reads them, so a signal day without a stored rate
        // (a European holiday) still shows the earlier one the server would use.
        var rates = (await ExchangeRates.ForPairAsync(db, strategy.Currency, asset.Currency, signal.SignalDate, cancellationToken))
            .ReferenceDays(strategy.Currency, asset.Currency, signal.SignalDate, today);

        SignalAnswer? answer = null;
        if (signal.Status == SignalStatus.Confirmed)
        {
            var trade = await db.UserTrades.AsNoTracking().FirstOrDefaultAsync(t => t.SignalId == signal.Id && t.DeletedAt == null, cancellationToken);
            answer = new SignalAnswer(signal.Status, null, trade?.TradeDate, trade?.Price, trade?.Amount, trade?.Shares, trade?.Fee);
        }
        else if (signal.Status == SignalStatus.Skipped) answer = new SignalAnswer(signal.Status, signal.SkipReason, null, null, null, null, null);

        var reason = EmailTexts.FactSentence(SignalLine.From(signal, strategy, asset), user.Locale);
        var canSnooze = strategy.TemplateKey != TemplateKeys.Ai;
        var movedOn = await MovedOnAsync(db, signal, cancellationToken);
        return new SignalDetail(signal.Id, strategy.Id, strategy.Name, texts.Name, strategy.Currency, asset.Name, asset.Currency, signal.StepNo,
            signal.MaxSteps, signal.StepAmount, signal.SignalDate, signal.ClosePrice, reason, signal.Status, signal.SnoozedUntil, signal.EndedOn,
            today, prefill, rates, answer, signal.Side, canSnooze, asset.Id,
            OnStockList: TemplateCatalog.Find(strategy.TemplateKey)?.InvestsIn == InvestsIn.StockList, MovedOn: movedOn);
    }
}
