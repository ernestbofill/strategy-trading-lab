using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Ai;

/// <summary>A strategy with its owner's local date, which decides when its rounds are due.</summary>
public record AiStrategyDay(Strategy Strategy, DateOnly Today);

/// <summary>What the daily AI check did.</summary>
public record AiDailySummary(int Strategies, int NewTrades, int Missed, int Failed);

/// <summary>A planned round: what was read, the stocks it was read against, and the steps it gives.</summary>
public record AiPlan(string Answer, AiReading Reading, IReadOnlyList<AiStock> Stocks, IReadOnlyList<AiPick> Picks, AiNoStep? Short, Budget Budget);

/// <summary>
/// The AI rounds of <i>Your AI's top picks</i> strategies (spec: AI round): keeps each strategy's round due, reads an answer
/// into steps, turns a round into signals, and once a day makes their signal trades and closes the signals whose time is up.
/// The app runs no AI: the user's own AI writes the answer, and the app applies the strategy's rule to its scores.
/// </summary>
public class AiRounds(AppDbContext db, TimeProvider time, ILogger<AiRounds> logger)
{
    /// <summary>The longest AI model name kept.</summary>
    public const int MaxModel = 60;

    public static bool IsAi(Strategy strategy) => strategy.TemplateKey == TemplateCatalog.Ai.Key;

    /// <summary>
    /// Makes sure each running AI strategy has its round due: the first on its start day, then one every <c>EveryDays</c>
    /// after the last scheduled one's due date (extra rounds don't move the schedule). A round not run by its last day is skipped. Idempotent, and safe when two run at
    /// once: the database keeps one round per due date and one due round per strategy.
    /// </summary>
    public async Task SweepAsync(IEnumerable<AiStrategyDay> strategies, CancellationToken cancellationToken)
    {
        foreach (var (strategy, today) in strategies)
        {
            if (!IsAi(strategy) || strategy.Status != StrategyStatus.Running) continue;
            var parameters = AiRoundRules.Params(strategy);
            try
            {
                var rounds = await db.AiRounds.Where(r => r.StrategyId == strategy.Id && !r.Extra).OrderBy(r => r.DueOn).ToListAsync(cancellationToken);
                var open = rounds.FirstOrDefault(r => r.Status == AiRoundStatus.Due);
                if (open is null)
                {
                    open = new AiRound
                    {
                        StrategyId = strategy.Id, Status = AiRoundStatus.Due,
                        // The first round is due on the start day; a strategy started before AI rounds existed starts from today,
                        // not with a backlog of rounds it could never have run.
                        DueOn = rounds.Count > 0 ? AiRoundRules.NextDue(rounds[^1].DueOn, parameters)
                            : AiRoundRules.LastDay(strategy.StartedOn, parameters) < today ? today : strategy.StartedOn,
                    };
                    db.AiRounds.Add(open);
                    await db.SaveChangesAsync(cancellationToken);
                }
                while (AiRoundRules.LastDay(open.DueOn, parameters) < today)
                {
                    // One write each, so the one-due-round index never sees two at once.
                    // Only while it is still due: a round just done by the user must stay done.
                    var skipped = await db.AiRounds.Where(r => r.Id == open.Id && r.Status == AiRoundStatus.Due)
                        .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, AiRoundStatus.Skipped), cancellationToken);
                    if (skipped == 0) break;
                    open = new AiRound { StrategyId = strategy.Id, Status = AiRoundStatus.Due, DueOn = AiRoundRules.NextDue(open.DueOn, parameters) };
                    db.AiRounds.Add(open);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            // Another request moved the same round on: what it wrote stands.
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
            }
            catch (DbUpdateConcurrencyException)
            {
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>The strategy's round that is due, or the next one. Null for a strategy that isn't a running AI one.</summary>
    public async Task<AiRound?> CurrentAsync(Strategy strategy, DateOnly today, CancellationToken cancellationToken)
    {
        if (!IsAi(strategy) || strategy.Status != StrategyStatus.Running) return null;
        await SweepAsync([new(strategy, today)], cancellationToken);
        return await db.AiRounds.AsNoTracking().SingleOrDefaultAsync(r => r.StrategyId == strategy.Id && r.Status == AiRoundStatus.Due, cancellationToken);
    }

    /// <summary>The stocks a round scores: the strategy's ticked ones that can signal, by name (the prompt's order).</summary>
    public async Task<List<AiStock>> StocksAsync(Strategy strategy, CancellationToken cancellationToken)
    {
        var ids = await db.StrategyAssets.Where(a => a.StrategyId == strategy.Id && a.Active && a.RemovedAt == null)
            .Select(a => a.AssetId).ToListAsync(cancellationToken);
        var stocks = await db.Assets.AsNoTracking().Where(a => ids.Contains(a.Id) && a.Active)
            .Select(a => new AiStock(a.Id, a.Symbol, a.Name)).ToListAsync(cancellationToken);
        return [.. stocks.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Symbol, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Reads an answer and works out the steps it would give now: the rules' steps so far, the budget the strategy's signals
    /// use, and which stocks can't get a signal on the last finished session (no price yet, or one already that day).
    /// </summary>
    public async Task<AiPlan> PlanAsync(Strategy strategy, string? answer, string locale, CancellationToken cancellationToken)
    {
        var stocks = await StocksAsync(strategy, cancellationToken);
        var reading = AiAnswerReader.Read(answer, stocks, locale);
        if (!reading.Ok) return new AiPlan(answer ?? "", reading, stocks, [], null, strategy.Budget);

        var session = UsSession.LastComplete(time.GetUtcNow());
        var signals = await db.Signals.AsNoTracking().Where(s => s.StrategyId == strategy.Id && s.Side == TradeSide.Buy)
            .Select(s => new { s.AssetId, s.SignalDate, s.Status, s.StepAmount }).ToListAsync(cancellationToken);
        var steps = signals.GroupBy(s => s.AssetId).ToDictionary(g => g.Key, g => g.Count());
        // As the daily check counts it: confirmed and waiting signals use their step; skipped and missed ones none.
        var used = signals.Where(s => s.Status is SignalStatus.Confirmed or SignalStatus.Waiting).Sum(s => s.StepAmount ?? 0);
        var ids = stocks.Select(s => s.AssetId).ToList();
        var priced = await db.Assets.Where(a => ids.Contains(a.Id) && db.Prices.Any(p => p.AssetId == a.Id && p.Date <= session))
            .Select(a => a.Id).ToListAsync(cancellationToken);
        var unavailable = ids.Except(priced).Concat(signals.Where(s => s.SignalDate == session).Select(s => s.AssetId)).ToHashSet();

        var parameters = AiRoundRules.Params(strategy);
        var (picks, missing) = AiRoundRules.Pick(reading.Scores, steps, stocks.ToDictionary(s => s.AssetId, s => s.Name), strategy.Budget, used,
            parameters.TopN, unavailable);
        return new AiPlan(answer!, reading, stocks, picks, missing, strategy.Budget);
    }

    /// <summary>The scores as the screens show them, with the step each one gets.</summary>
    public static List<AiScoreDto> ScoresOf(AiPlan plan)
    {
        var steps = plan.Picks.ToDictionary(p => p.AssetId, p => p.StepNo);
        var stocks = plan.Stocks.ToDictionary(s => s.AssetId);
        return [.. plan.Reading.Scores.Select(s => new AiScoreDto(s.AssetId, stocks[s.AssetId].Symbol, stocks[s.AssetId].Name, s.Overall,
            s.News, s.Mood, s.Figures, s.Reason, steps.TryGetValue(s.AssetId, out var step) ? step : null))];
    }

    /// <summary>
    /// "Use these scores": the round is done, with what the user copied and pasted and the scores read; each pick gets a buy
    /// signal (simulation: confirmed and bought at once at the latest stored price; live: waiting for the user); the next round
    /// is due <c>EveryDays</c> after this one's due date; and, when the user shares, an anonymous copy is kept apart. An
    /// <paramref name="extra"/> round is a new done round dated <paramref name="today"/>, and <paramref name="round"/> (the
    /// scheduled one, not due yet) stays as it is. One transaction; null when the round was done meanwhile (a second tap,
    /// another device).
    /// </summary>
    public async Task<AiRoundResult?> SubmitAsync(Strategy strategy, User user, AiRound round, bool extra, DateOnly today, AiPlan plan, string prompt,
        string model, bool? share, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var session = UsSession.LastComplete(now);
        var parameters = AiRoundRules.Params(strategy);
        // A signal's time is up when the next scheduled round is due: after an extra round, the one already scheduled.
        var nextDue = extra ? round.DueOn : AiRoundRules.NextDue(round.DueOn, parameters);
        var live = strategy.Mode == StrategyMode.Live;
        var picked = plan.Picks.ToDictionary(p => p.AssetId, p => p.StepNo);
        var stocks = plan.Stocks.ToDictionary(s => s.AssetId);
        // A stock tied with the top score is "the top score" too, whichever the tie-break picked first.
        var ranked = plan.Reading.Scores.ToDictionary(s => s.AssetId, s => 1 + plan.Reading.Scores.Count(other => other.Overall > s.Overall));
        var read = plan.Reading.Scores.ToDictionary(s => s.AssetId);
        var scores = plan.Reading.Scores.Select(s => new AiRoundScore(s.AssetId, stocks[s.AssetId].Symbol, stocks[s.AssetId].Name, s.Overall,
            s.News, s.Mood, s.Figures, s.Reason, picked.TryGetValue(s.AssetId, out var step) ? step : null)).ToList();

        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var scoresJson = JsonSerializer.SerializeToDocument(scores, JsonSerializerOptions.Web);
        var roundId = round.Id;
        if (extra)
        {
            // Added with the rest: a second extra round that day breaks the unique index and is refused below.
            var added = new AiRound
            {
                StrategyId = strategy.Id, DueOn = today, Extra = true, Status = AiRoundStatus.Done, DoneAt = now, SessionDate = session,
                PromptVersion = AiPrompt.Version, PromptEdited = parameters.Instructions != null, PromptText = prompt, AiModel = model,
                Answer = plan.Answer, Scores = scoresJson,
            };
            db.AiRounds.Add(added);
            roundId = added.Id;
        }
        else
        {
            var done = await db.AiRounds.Where(r => r.Id == round.Id && r.Status == AiRoundStatus.Due)
                .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, AiRoundStatus.Done).SetProperty(x => x.DoneAt, now)
                    .SetProperty(x => x.SessionDate, session).SetProperty(x => x.PromptVersion, AiPrompt.Version)
                    .SetProperty(x => x.PromptEdited, parameters.Instructions != null).SetProperty(x => x.PromptText, prompt)
                    .SetProperty(x => x.AiModel, model).SetProperty(x => x.Answer, plan.Answer)
                    .SetProperty(x => x.Scores, scoresJson), cancellationToken);
            if (done == 0) return null;
        }

        // A simulation buys at once, at the latest price stored (the endpoint refreshes the picks' prices first).
        var ids = picked.Keys.ToList();
        var assets = live ? new Dictionary<Guid, Asset>()
            : await db.Assets.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var rates = live ? null : await ExchangeRates.LoadAsync(db, session, cancellationToken);

        var signals = new List<Signal>();
        foreach (var pick in plan.Picks)
        {
            var close = await db.Prices.Where(p => p.AssetId == pick.AssetId && p.Date <= session).OrderByDescending(p => p.Date)
                .Select(p => p.Close).FirstAsync(cancellationToken);
            var score = read[pick.AssetId];
            var facts = new JsonObject
            {
                ["score"] = score.Overall, ["rank"] = ranked[pick.AssetId], ["roundId"] = roundId.ToString(),
                ["tradeAfter"] = AiRoundRules.TradeAfter(now).ToString("O"), ["endsOn"] = nextDue.ToString("O"),
            };
            var signal = new Signal
            {
                StrategyId = strategy.Id, AssetId = pick.AssetId, Side = TradeSide.Buy, StepNo = pick.StepNo, MaxSteps = strategy.Budget.MaxSteps,
                StepAmount = strategy.Budget.Step, SignalDate = session, ClosePrice = close,
                Facts = JsonSerializer.SerializeToDocument(facts), Status = live ? SignalStatus.Waiting : SignalStatus.Confirmed, AnsweredAt = live ? null : now,
            };
            db.Signals.Add(signal);
            signals.Add(signal);
            if (rates is not null && await LatestTradeAsync(signal, strategy, assets[pick.AssetId], session, rates, cancellationToken) is { } trade)
                db.SignalTrades.Add(trade);
            // The strategy page shows the pick today, as the daily check's own signals.
            await SaveEvaluationAsync(strategy.Id, pick.AssetId, session, EvaluationStatus.BuySignal, pick.StepNo, ScoreFacts(score.Overall), cancellationToken);
        }
        if (!extra) db.AiRounds.Add(new AiRound { StrategyId = strategy.Id, Status = AiRoundStatus.Due, DueOn = nextDue });

        // The user's choice as it stands now, with their row locked until this commits: turning sharing off on another device
        // either waits for this round (and then removes its shared copy) or is seen here.
        bool sharing;
        if (share is { } choice)
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.AiSharing, choice).SetProperty(x => x.AiSharingChangedAt, now),
                cancellationToken);
            sharing = choice;
            // Turning sharing off removes what was shared before, whichever screen or device it comes from.
            if (!choice) await ForgetSharedAsync(user.Id, cancellationToken);
        }
        else
            sharing = await db.Database.SqlQuery<bool>($"SELECT ai_sharing AS \"Value\" FROM users WHERE id = {user.Id} FOR UPDATE").SingleAsync(cancellationToken);
        Guid? sharedId = null;
        if (sharing)
        {
            var on = AiPrompt.On(parameters);
            // A random id: a version 7 one would carry the exact time the round was done.
            sharedId = Guid.NewGuid();
            db.SharedAiRounds.Add(new SharedAiRound
            {
                Id = sharedId.Value,
                // The day only (no exact time to match against the round's), and nothing about the user, the strategy, its budget
                // or what the user did.
                RoundOn = UsSession.Today(now),
                CreatedAt = new DateTimeOffset(UsSession.Today(now).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                AiModel = model,
                PromptVersion = AiPrompt.Version,
                PromptEdited = parameters.Instructions != null,
                Sources = JsonSerializer.SerializeToDocument(on.Select(s => s.Key)),
                Stocks = JsonSerializer.SerializeToDocument(plan.Stocks.Select(s => s.Symbol)),
                Answer = AiSharingScrub.Scrub(plan.Answer),
                Scores = JsonSerializer.SerializeToDocument(scores.Select(s => new { s.Symbol, s.Overall, s.News, s.Mood, s.Figures }), JsonSerializerOptions.Web),
            });
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // The next round, or a signal on that day, exists already: done by another request meanwhile.
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return null;
        }
        if (sharedId is not null)
            await db.AiRounds.Where(r => r.Id == roundId).ExecuteUpdateAsync(r => r.SetProperty(x => x.SharedRoundId, sharedId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new AiRoundResult(roundId, [.. signals.Select(s => s.Id)], strategy.Mode);
    }

    /// <summary>
    /// A simulation's buy for a new signal at the latest stored price (today's bar while the market is open), dated that bar's day,
    /// or the signal's session when the bar is older (a holiday). Null without the rate or anything to buy with: the daily check
    /// then buys it at the next open, as before.
    /// </summary>
    async Task<SignalTrade?> LatestTradeAsync(Signal signal, Strategy strategy, Asset asset, DateOnly session, ExchangeRates rates,
        CancellationToken cancellationToken)
    {
        var latest = await db.Prices.AsNoTracking().Where(p => p.AssetId == signal.AssetId).OrderByDescending(p => p.Date)
            .Select(p => new { p.Date, p.Close }).FirstAsync(cancellationToken);
        var date = latest.Date > session ? latest.Date : session;
        return rates.Between(strategy.Currency, asset.Currency, date) is { } fx
            ? StrategyEvaluator.BuyTrade(signal, strategy, date, latest.Close, fx, signal.StepAmount ?? 0)
            : null;
    }

    /// <summary>
    /// Skips the round that is due, in either mode: no signal, and the next round is due <c>EveryDays</c> after this one's due
    /// date, as when its week runs out. False when the round was done or skipped meanwhile.
    /// </summary>
    public async Task<bool> SkipAsync(Strategy strategy, AiRound round, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var skipped = await db.AiRounds.Where(r => r.Id == round.Id && r.Status == AiRoundStatus.Due)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, AiRoundStatus.Skipped), cancellationToken);
        if (skipped == 0) return false;
        db.AiRounds.Add(new AiRound { StrategyId = strategy.Id, Status = AiRoundStatus.Due, DueOn = AiRoundRules.NextDue(round.DueOn, AiRoundRules.Params(strategy)) });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return true;
    }

    /// <summary>Stores a stock's status for a session, replacing the daily check's row for that day if there is one.</summary>
    async Task SaveEvaluationAsync(Guid strategyId, Guid assetId, DateOnly date, EvaluationStatus status, int? stepNo, JsonDocument facts,
        CancellationToken cancellationToken)
    {
        var row = await db.DailyEvaluations.FindAsync([strategyId, assetId, date], cancellationToken);
        if (row is null)
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = strategyId, AssetId = assetId, Date = date, Status = status, StepNo = stepNo, Facts = facts });
        else
        {
            row.Status = status;
            row.StepNo = stepNo;
            row.Facts = facts;
        }
    }

    static JsonDocument ScoreFacts(decimal? score) =>
        JsonSerializer.SerializeToDocument(score is null ? new JsonObject() : new JsonObject { ["score"] = score });

    /// <summary>
    /// The daily AI check, after the price refresh: rounds whose week ran out are skipped and the next ones made due; each AI
    /// signal's trade is recorded at the first open after its round, once that day's price is in; a signal still waiting when
    /// the next round is due is missed; and each stock's status for the last finished session is stored.
    /// </summary>
    public async Task<AiDailySummary> RunDailyAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var session = UsSession.LastComplete(now);
        var rows = await (from strategy in db.Strategies.AsNoTracking().Include(s => s.Assets)
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          where strategy.Status == StrategyStatus.Running && strategy.TemplateKey == TemplateCatalog.Ai.Key
                          select new { strategy, user.TimeZone }).ToListAsync(cancellationToken);
        var rates = await ExchangeRates.LoadAsync(db, null, cancellationToken);
        int trades = 0, missed = 0, failed = 0;
        foreach (var row in rows)
        {
            var today = StrategyEndpoints.Today(row.TimeZone, now);
            try
            {
                await SweepAsync([new(row.strategy, today)], cancellationToken);
                var (newTrades, newMissed) = await DailyAsync(row.strategy, today, session, rates, cancellationToken);
                trades += newTrades;
                missed += newMissed;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                failed++;
                logger.LogError(error, "The AI check of strategy {Strategy} failed.", row.strategy.Id);
            }
        }
        return new AiDailySummary(rows.Count, trades, missed, failed);
    }

    async Task<(int Trades, int Missed)> DailyAsync(Strategy strategy, DateOnly today, DateOnly session, ExchangeRates rates, CancellationToken cancellationToken)
    {
        var signals = await db.Signals.Where(s => s.StrategyId == strategy.Id && s.Side == TradeSide.Buy).ToListAsync(cancellationToken);
        var traded = (await db.SignalTrades.Where(t => t.StrategyId == strategy.Id).Select(t => t.SignalId).ToListAsync(cancellationToken)).ToHashSet();
        var assetIds = signals.Select(s => s.AssetId).Concat(strategy.Assets.Select(a => a.AssetId)).Distinct().ToList();
        var assets = await db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);

        // Each signal's trade, at the first open after its round, once that price is stored.
        var trades = 0;
        foreach (var signal in signals.Where(s => !traded.Contains(s.Id)))
        {
            var after = DateFact(signal, "tradeAfter") ?? signal.SignalDate;
            var next = await db.Prices.AsNoTracking().Where(p => p.AssetId == signal.AssetId && p.Date > after).OrderBy(p => p.Date)
                .Select(p => new { p.Date, p.Open }).FirstOrDefaultAsync(cancellationToken);
            if (next is null || !assets.TryGetValue(signal.AssetId, out var asset)
                || rates.Between(strategy.Currency, asset.Currency, next.Date) is not { } fx) continue;
            if (StrategyEvaluator.BuyTrade(signal, strategy, next.Date, next.Open, fx, signal.StepAmount ?? 0) is not { } trade) continue;
            db.SignalTrades.Add(trade);
            trades++;
        }

        // A signal's time is up when the next round is due.
        foreach (var signal in signals.Where(s => s.EndedOn is null))
            if (DateFact(signal, "endsOn") is { } ends && ends <= today) signal.EndedOn = ends;

        // Where each ticked stock stands at the last session's close: its signal that day, its steps, or its last score.
        var lastScores = await LastScoresAsync(strategy.Id, cancellationToken);
        var maxSteps = strategy.Budget.MaxSteps ?? 1;
        foreach (var assetId in strategy.Assets.Where(a => a.Active && a.RemovedAt is null).Select(a => a.AssetId))
        {
            var own = signals.Where(s => s.AssetId == assetId).ToList();
            var todays = own.FirstOrDefault(s => s.SignalDate == session);
            var status = todays is not null ? EvaluationStatus.BuySignal
                : own.Count >= maxSteps ? EvaluationStatus.Holding
                : own.Count > 0 ? EvaluationStatus.Building
                : lastScores.ContainsKey(assetId) ? EvaluationStatus.Watching
                : EvaluationStatus.NotCandidate;
            var facts = ScoreFacts(lastScores.TryGetValue(assetId, out var score) ? score : null);
            await SaveEvaluationAsync(strategy.Id, assetId, session, status, todays?.StepNo, facts, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        // Read in the database, so an answer saved since these rows were read stands (as the daily check does).
        var missed = await db.Signals.Where(s => s.StrategyId == strategy.Id && s.EndedOn != null && s.Status == SignalStatus.Waiting)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Missed).SetProperty(x => x.SnoozedUntil, (DateOnly?)null), cancellationToken);
        db.ChangeTracker.Clear();
        return (trades, missed);
    }

    /// <summary>Each stock's overall score in the strategy's latest done round that scored it.</summary>
    public async Task<Dictionary<Guid, decimal>> LastScoresAsync(Guid strategyId, CancellationToken cancellationToken) =>
        LastScores(await db.AiRounds.AsNoTracking().Where(r => r.StrategyId == strategyId && r.Status == AiRoundStatus.Done).ToListAsync(cancellationToken));

    static Dictionary<Guid, decimal> LastScores(IEnumerable<AiRound> rounds)
    {
        var last = new Dictionary<Guid, decimal>();
        foreach (var round in rounds.Where(r => r.Status == AiRoundStatus.Done).OrderByDescending(r => r.DoneAt))
            foreach (var score in Read(round.Scores))
                last.TryAdd(score.AssetId, score.Overall);
        return last;
    }

    /// <summary>
    /// Turning sharing off also removes what the user shared. A shared round has no key to its user: the user's own round points
    /// to it (<c>shared_round_id</c>, set to null when the shared copy goes).
    /// </summary>
    public async Task<int> ForgetSharedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var shared = await (from round in db.AiRounds.AsNoTracking()
                            join strategy in db.Strategies.AsNoTracking() on round.StrategyId equals strategy.Id
                            where strategy.UserId == userId && round.SharedRoundId != null
                            select round.SharedRoundId!.Value).ToListAsync(cancellationToken);
        return shared.Count == 0 ? 0 : await db.SharedAiRounds.Where(s => shared.Contains(s.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public static IReadOnlyList<AiRoundScore> Read(JsonDocument? scores) =>
        scores?.Deserialize<List<AiRoundScore>>(JsonSerializerOptions.Web) ?? [];

    static DateOnly? DateFact(Signal signal, string name) =>
        signal.Facts.RootElement.ValueKind == JsonValueKind.Object && signal.Facts.RootElement.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String && DateOnly.TryParse(value.GetString(), out var date) ? date : null;

    /// <summary>
    /// The history: every round, newest first, and each done round's picks against the rest: the average change of their
    /// prices from the round's session close to the latest close, in each stock's own currency.
    /// </summary>
    public async Task<(List<AiRoundItem> Rounds, PicksVsRest? Total)> HistoryAsync(Strategy strategy, CancellationToken cancellationToken)
    {
        var rounds = await db.AiRounds.AsNoTracking().Where(r => r.StrategyId == strategy.Id).OrderByDescending(r => r.DueOn).ToListAsync(cancellationToken);
        var changes = await ChangesAsync(rounds, cancellationToken);
        var items = rounds.Select(r =>
        {
            var scores = Read(r.Scores);
            var (picks, rest) = changes.GetValueOrDefault(r.Id);
            return new AiRoundItem(r.Id, r.DueOn, r.Extra, r.Status, r.DoneAt, r.SessionDate, r.AiModel, r.PromptVersion, r.PromptEdited, r.PromptText, r.Answer,
                [.. scores.Select(s => new AiScoreDto(s.AssetId, s.Symbol, s.Name, s.Overall, s.News, s.Mood, s.Figures, s.Reason, s.Step))], picks, rest);
        }).ToList();
        return (items, Total(changes.Values));
    }

    /// <summary>The strategy page's line (the next round and the picks against the rest), and each stock's last score.</summary>
    public async Task<(AiRoundsSummary Summary, Dictionary<Guid, decimal> LastScores)> SummaryAsync(Strategy strategy, DateOnly today,
        CancellationToken cancellationToken)
    {
        if (strategy.Status == StrategyStatus.Running) await SweepAsync([new(strategy, today)], cancellationToken);
        var rounds = await db.AiRounds.AsNoTracking().Where(r => r.StrategyId == strategy.Id).ToListAsync(cancellationToken);
        // A round of an ended strategy is never due.
        var current = strategy.Status == StrategyStatus.Running ? rounds.FirstOrDefault(r => r.Status == AiRoundStatus.Due) : null;
        var changes = await ChangesAsync(rounds, cancellationToken);
        var summary = new AiRoundsSummary(current?.DueOn, current is not null && current.DueOn <= today, rounds.Count(r => r.Status == AiRoundStatus.Done),
            rounds.Count(r => r.Status == AiRoundStatus.Skipped), Total(changes.Values));
        return (summary, LastScores(rounds));
    }

    static PicksVsRest? Total(IEnumerable<(decimal? Picks, decimal? Others)> changes)
    {
        var both = changes.Where(c => c.Picks is not null && c.Others is not null).ToList();
        return both.Count == 0 ? null
            : new PicksVsRest(Math.Round(both.Average(c => c.Picks!.Value), 1), Math.Round(both.Average(c => c.Others!.Value), 1), both.Count);
    }

    async Task<Dictionary<Guid, (decimal? Picks, decimal? Others)>> ChangesAsync(IReadOnlyList<AiRound> rounds, CancellationToken cancellationToken)
    {
        var done = rounds.Where(r => r.Status == AiRoundStatus.Done && r.SessionDate is not null).Select(r => (r.Id, Session: r.SessionDate!.Value, Scores: Read(r.Scores))).ToList();
        if (done.Count == 0) return [];
        // Two closes per stock and round: its last close on or before the round's session, and its latest.
        var ids = done.SelectMany(r => r.Scores.Select(s => s.AssetId)).Distinct().ToList();
        var latest = await LastClosesAsync(ids, null, cancellationToken);
        var atSession = new Dictionary<DateOnly, Dictionary<Guid, (DateOnly Date, decimal Close)>>();
        foreach (var session in done.Select(r => r.Session).Distinct())
            atSession[session] = await LastClosesAsync([.. done.Where(r => r.Session == session).SelectMany(r => r.Scores.Select(s => s.AssetId)).Distinct()],
                session, cancellationToken);

        decimal? Change(Guid assetId, DateOnly session)
        {
            if (!atSession[session].TryGetValue(assetId, out var then) || !latest.TryGetValue(assetId, out var now)) return null;
            if (now.Date <= then.Date) return null; // No close since the round: nothing to compare yet.
            return (now.Close / then.Close - 1) * 100;
        }
        decimal? Average(IEnumerable<decimal?> values) => values.Where(v => v is not null).Select(v => v!.Value).ToList() is { Count: > 0 } list
            ? Math.Round(list.Average(), 1) : null;

        return done.ToDictionary(r => r.Id, r => (
            Average(r.Scores.Where(s => s.Step is not null).Select(s => Change(s.AssetId, r.Session))),
            Average(r.Scores.Where(s => s.Step is null).Select(s => Change(s.AssetId, r.Session)))));
    }

    /// <summary>Each asset's last close on or before <paramref name="on"/> (the latest when null), within the look-back.</summary>
    async Task<Dictionary<Guid, (DateOnly Date, decimal Close)>> LastClosesAsync(List<Guid> ids, DateOnly? on, CancellationToken cancellationToken)
    {
        var rows = await db.Prices.AsNoTracking().Where(p => ids.Contains(p.AssetId) && (on == null || p.Date <= on))
            .GroupBy(p => p.AssetId)
            .Select(g => g.OrderByDescending(p => p.Date).Select(p => new { p.AssetId, p.Date, p.Close }).First())
            .ToListAsync(cancellationToken);
        return rows.Where(r => on is null || r.Date >= on.Value.AddDays(-ExchangeRates.LookBackDays)).ToDictionary(r => r.AssetId, r => (r.Date, r.Close));
    }
}
