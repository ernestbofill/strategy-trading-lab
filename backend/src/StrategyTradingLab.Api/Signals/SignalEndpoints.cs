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

            var rates = await ExchangeRates.LoadAsync(db, signal.SignalDate, cancellationToken);
            var (trade, errors) = SignalRules.CheckTrade(request, signal.SignalDate, StrategyEndpoints.Today(user, time),
                day => rates.Between(strategy.Currency, asset.Currency, day));
            if (trade is null) return TypedResults.ValidationProblem(errors);

            db.UserTrades.Add(new UserTrade
            {
                StrategyId = strategy.Id, AssetId = asset.Id, TradeDate = trade.Date, Side = signal.Side, Price = trade.Price, EnteredAs = trade.EnteredAs,
                Amount = trade.Amount, Shares = trade.Shares, Fee = trade.Fee, FxRate = trade.FxRate, FxSource = trade.FxSource,
                Source = TradeSource.Signal, SignalId = signal.Id,
            });
            Answer(signal, SignalStatus.Confirmed, null, time);
            return await SaveAsync(db, cancellationToken) ? TypedResults.NoContent() : TypedResults.Conflict(AlreadyAnswered);
        })
        .WithName("ConfirmSignal")
        .WithSummary("\"I did it\": records the user's trade for the signal.");

        group.MapPost("/{id:guid}/skip", async Task<Results<NoContent, NotFound, Conflict<string>>> (
            Guid id, SignalSkipRequest request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } found) return TypedResults.NotFound();
            if (!Answerable(found.Signal)) return TypedResults.Conflict(AlreadyAnswered);
            Answer(found.Signal, SignalStatus.Skipped, request.Reason, time);
            return await SaveAsync(db, cancellationToken) ? TypedResults.NoContent() : TypedResults.Conflict(AlreadyAnswered);
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
            var until = SignalRules.NextWeekday(StrategyEndpoints.Today(user, time));
            found.Signal.SnoozedUntil = until;
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(new SnoozeResult(until));
        })
        .WithName("SnoozeSignal")
        .WithSummary("Snooze: the signal stays waiting and the user is reminded the next weekday.");
    }

    /// <summary>A buy signal of one of the user's live strategies, tracked for changes; null for anything else (also another user's).</summary>
    static async Task<(Signal Signal, Strategy Strategy, Asset Asset)?> FindAsync(AppDbContext db, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var row = await (from signal in db.Signals
                         join strategy in db.Strategies on signal.StrategyId equals strategy.Id
                         join asset in db.Assets on signal.AssetId equals asset.Id
                         where signal.Id == id && strategy.UserId == userId && strategy.Mode == StrategyMode.Live && signal.Side == TradeSide.Buy
                         select new { signal, strategy, asset }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.signal, row.strategy, row.asset);
    }

    static bool Answerable(Signal signal) => signal.Status is SignalStatus.Waiting or SignalStatus.Missed;

    static void Answer(Signal signal, SignalStatus status, SkipReason? reason, TimeProvider time)
    {
        signal.Status = status;
        signal.SkipReason = reason;
        signal.AnsweredAt = time.GetUtcNow();
        signal.SnoozedUntil = null; // Only a waiting signal can be snoozed (the table's check).
    }

    /// <summary>Saves the answer; false when another save answered the signal first (its trade's unique index).</summary>
    static async Task<bool> SaveAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OneTradeIndex })
        {
            return false;
        }
    }

    static async Task<SignalDetail> DetailAsync(AppDbContext db, (Signal Signal, Strategy Strategy, Asset Asset) found, User user, DateOnly today,
        CancellationToken cancellationToken)
    {
        var (signal, strategy, asset) = found;
        var texts = TemplateTexts.For(TemplateCatalog.Find(strategy.TemplateKey)!, user.Locale);

        // The trade day: the first trading day after the signal once its price is stored, else the next weekday.
        var next = await db.Prices.AsNoTracking().Where(p => p.AssetId == asset.Id && p.Date > signal.SignalDate).OrderBy(p => p.Date)
            .Select(p => new { p.Date, p.Open }).FirstOrDefaultAsync(cancellationToken);
        var prefill = new TradePrefill(next?.Date ?? SignalRules.NextWeekday(signal.SignalDate), next?.Open,
            signal.StepAmount is { } step && step > TemplateCatalog.Fee.Fixed + 0.01m ? TemplateCatalog.Fee.Split(step).Amount : null);

        var exchange = await ExchangeRates.LoadAsync(db, signal.SignalDate, cancellationToken);
        var rates = new List<RateDay>();
        for (var day = signal.SignalDate; day <= today; day = day.AddDays(1))
            if (exchange.Between(strategy.Currency, asset.Currency, day) is { } rate) rates.Add(new RateDay(day, rate));

        SignalAnswer? answer = null;
        if (signal.Status == SignalStatus.Confirmed)
        {
            var trade = await db.UserTrades.AsNoTracking().FirstOrDefaultAsync(t => t.SignalId == signal.Id && t.DeletedAt == null, cancellationToken);
            answer = new SignalAnswer(signal.Status, null, trade?.TradeDate, trade?.Price, trade?.Amount, trade?.Shares, trade?.Fee);
        }
        else if (signal.Status == SignalStatus.Skipped) answer = new SignalAnswer(signal.Status, signal.SkipReason, null, null, null, null, null);

        var reason = EmailTexts.FactSentence(new SignalLine(strategy.Id, signal.Id, strategy.Name, strategy.Mode, strategy.TemplateKey, asset.Name,
            asset.Currency, strategy.Currency, signal.SignalDate, signal.ClosePrice, signal.StepNo, signal.MaxSteps, signal.StepAmount, signal.Facts.RootElement));
        return new SignalDetail(signal.Id, strategy.Id, strategy.Name, texts.Name, strategy.Currency, asset.Name, asset.Currency, signal.StepNo,
            signal.MaxSteps, signal.StepAmount, signal.SignalDate, signal.ClosePrice, reason, signal.Status, signal.SnoozedUntil, signal.EndedOn,
            today, prefill, rates, answer);
    }
}
