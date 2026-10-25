using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Notifications;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Signals;

/// <summary>
/// Confirm a signal (spec: Confirm a signal), live strategies only: the signal with what the form starts with, and its three
/// answers: the trade the user made, a skip, or a snooze until the next weekday. A waiting or missed signal can be answered
/// (later, with the real date); an answered one can't be answered again.
/// </summary>
public static class SignalEndpoints
{
    /// <summary>The unique index on user_trades (signal_id) WHERE deleted_at IS NULL: one trade per signal, also when two saves race.</summary>
    const string OneTradeIndex = "ix_user_trades_signal_id";

    const string AlreadyAnswered = "This signal is already answered.";

    public static void MapSignals(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/signals").WithTags("Signals").RequireUser();

        group.MapGet("/{id:guid}", async Task<Results<Ok<SignalDetail>, NotFound>> (
            Guid id, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            return TypedResults.Ok(await DetailAsync(db, found, user, StrategyEndpoints.Today(user, time), cancellationToken));
        })
        .WithName("GetSignal")
        .WithSummary("A live signal, with what Confirm a signal's form starts with.");

        group.MapPost("/{id:guid}/trade", async Task<Results<NoContent, ValidationProblem, NotFound, Conflict<string>>> (
            Guid id, SignalTradeRequest request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            var (signal, strategy, asset) = found;
            if (!Answerable(signal)) return TypedResults.Conflict(AlreadyAnswered);

            // The same currency needs no rate (it is 1): nothing to load.
            var rates = strategy.Currency == asset.Currency ? new ExchangeRates([]) : await ExchangeRates.LoadAsync(db, signal.SignalDate, cancellationToken);
            var (trade, errors) = SignalRules.CheckTrade(request, signal.SignalDate, StrategyEndpoints.Today(user, time),
                day => rates.Between(strategy.Currency, asset.Currency, day));
            if (trade is null) return TypedResults.ValidationProblem(errors);

            var userTrade = new UserTrade
            {
                StrategyId = strategy.Id, AssetId = asset.Id, TradeDate = trade.Date, Side = signal.Side, Price = trade.Price, EnteredAs = trade.EnteredAs,
                Amount = trade.Amount, Shares = trade.Shares, Fee = trade.Fee, FxRate = trade.FxRate, FxSource = trade.FxSource,
                Source = TradeSource.Signal, SignalId = signal.Id,
            };
            return await AnswerAsync(db, signal.Id, SignalStatus.Confirmed, null, userTrade, time, cancellationToken)
                ? TypedResults.NoContent() : TypedResults.Conflict(AlreadyAnswered);
        })
        .WithName("ConfirmSignal")
        .WithSummary("\"I did it\": records the user's trade for the signal.");

        group.MapPost("/{id:guid}/skip", async Task<Results<NoContent, NotFound, Conflict<string>>> (
            Guid id, SignalSkipRequest request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            if (!Answerable(found.Signal)) return TypedResults.Conflict(AlreadyAnswered);
            return await AnswerAsync(db, id, SignalStatus.Skipped, request.Reason, null, time, cancellationToken)
                ? TypedResults.NoContent() : TypedResults.Conflict(AlreadyAnswered);
        })
        .WithName("SkipSignal")
        .WithSummary("\"I skipped it\": uses no budget; the rules carry on as if the step was bought.");

        group.MapPost("/{id:guid}/snooze", async Task<Results<Ok<SnoozeResult>, NotFound, Conflict<string>>> (
            Guid id, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            // Only a waiting signal can be snoozed (the table's check): a missed one has nothing left to remind of.
            if (found.Signal.Status != SignalStatus.Waiting) return TypedResults.Conflict("Only a signal still waiting can be snoozed.");
            // A snooze promises a reminder: refuse it when none could be sent.
            if (found.Strategy.Status != StrategyStatus.Running) return TypedResults.Conflict("This strategy has ended, so there is no reminder to send.");
            if (!found.Strategy.Notifications) return TypedResults.Conflict("This strategy's notifications are off, so there is no reminder to send.");
            if (user.EmailVerifiedAt is null) return TypedResults.Conflict("Confirm your email address to get reminders.");
            // The next US market day (spec: a day is a US market day), counted from the later of New York's date and the user's,
            // so it is always after the user's own today: the reminder goes out from 07:00 of that day in their time zone.
            var newYork = UsSession.Today(time.GetUtcNow());
            var local = StrategyEndpoints.Today(user, time);
            var until = SignalRules.NextWeekday(local > newYork ? local : newYork);
            // Conditional, like the answers: the signal may have been answered or missed since it was read.
            var snoozed = await db.Signals.Where(s => s.Id == id && s.Status == SignalStatus.Waiting)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SnoozedUntil, until), cancellationToken);
            return snoozed == 1 ? TypedResults.Ok(new SnoozeResult(until)) : TypedResults.Conflict("Only a signal still waiting can be snoozed.");
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
    /// Answers the signal and stores its trade, if any, in one transaction; false when it was already answered. The status
    /// changes only where it is still waiting or missed in the database, and that update locks the row, so of two answers at
    /// once (a phone and a computer, or the daily check marking it missed) only one wins and none is overwritten.
    /// </summary>
    static async Task<bool> AnswerAsync(AppDbContext db, Guid signalId, SignalStatus status, SkipReason? reason, UserTrade? trade,
        TimeProvider time, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear(); // Only this method writes: nothing loaded to check the request may be saved by it.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = time.GetUtcNow();
        var answered = await db.Signals
            .Where(s => s.Id == signalId && (s.Status == SignalStatus.Waiting || s.Status == SignalStatus.Missed))
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
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OneTradeIndex })
            {
                return false;
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    static async Task<SignalDetail> DetailAsync(AppDbContext db, (Signal Signal, Strategy Strategy, Asset Asset) found, User user, DateOnly today,
        CancellationToken cancellationToken)
    {
        var (signal, strategy, asset) = found;
        var texts = TemplateTexts.For(TemplateCatalog.Find(strategy.TemplateKey)!, user.Locale);

        // The trade day: the first trading day after the signal once its price is stored, else the next weekday.
        var next = await db.Prices.AsNoTracking().Where(p => p.AssetId == asset.Id && p.Date > signal.SignalDate).OrderBy(p => p.Date)
            .Select(p => new { p.Date, p.Open }).FirstOrDefaultAsync(cancellationToken);
        var nextDay = next?.Date ?? SignalRules.NextWeekday(signal.SignalDate);
        decimal? prefillAmount = null, prefillShares = null;
        if (signal.Side == TradeSide.Buy)
        {
            var step = signal.StepAmount ?? strategy.Budget.Amount;
            if (step > TemplateCatalog.Fee.Fixed + 0.01m) prefillAmount = TemplateCatalog.Fee.Split(step).Amount;
        }
        else
        {
            var held = await db.UserTrades.AsNoTracking()
                .Where(t => t.StrategyId == strategy.Id && t.AssetId == asset.Id && t.DeletedAt == null)
                .SumAsync(t => t.Side == TradeSide.Buy ? t.Shares : -t.Shares, cancellationToken);
            if (held > 0) prefillShares = held;
        }
        var prefill = new TradePrefill(nextDay > today ? today : nextDay, next?.Open, prefillAmount, prefillShares);

        var rates = new List<RateDay>();
        // The screen shows no rate line when the currencies match, so nothing to load then.
        var exchange = strategy.Currency == asset.Currency ? null : await ExchangeRates.LoadAsync(db, signal.SignalDate, cancellationToken);
        if (exchange is not null)
        // Only days with both rates stored: another day (a weekend, a holiday, today before the rates are in) would repeat an
        // earlier rate under its own date. The form picks the latest on or before its date, as the server does.
        // From the look-back before the signal, as the server's own lookup reads them, so a signal day without a stored rate
        // (a European holiday) still shows the earlier one the server would use.
        for (var day = signal.SignalDate.AddDays(-ExchangeRates.LookBackDays); day <= today; day = day.AddDays(1))
            if (exchange.StoredOn(strategy.Currency, day) && exchange.StoredOn(asset.Currency, day)
                && day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && exchange.Between(strategy.Currency, asset.Currency, day) is { } rate)
                rates.Add(new RateDay(day, rate));

        SignalAnswer? answer = null;
        if (signal.Status == SignalStatus.Confirmed)
        {
            var trade = await db.UserTrades.AsNoTracking().FirstOrDefaultAsync(t => t.SignalId == signal.Id && t.DeletedAt == null, cancellationToken);
            answer = new SignalAnswer(signal.Status, null, trade?.TradeDate, trade?.Price, trade?.Amount, trade?.Shares, trade?.Fee);
        }
        else if (signal.Status == SignalStatus.Skipped) answer = new SignalAnswer(signal.Status, signal.SkipReason, null, null, null, null, null);

        var reason = EmailTexts.FactSentence(SignalLine.From(signal, strategy, asset));
        return new SignalDetail(signal.Id, strategy.Id, strategy.Name, texts.Name, strategy.Currency, asset.Name, asset.Currency, signal.StepNo,
            signal.MaxSteps, signal.StepAmount, signal.SignalDate, signal.ClosePrice, reason, signal.Status, signal.SnoozedUntil, signal.EndedOn,
            today, prefill, rates, answer, signal.Side);
    }
}
