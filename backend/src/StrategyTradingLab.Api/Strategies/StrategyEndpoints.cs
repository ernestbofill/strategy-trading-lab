using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Ai;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Notifications;
using StrategyTradingLab.Api.Signals;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

public static class StrategyEndpoints
{
    /// <summary>The unique index on (user_id, lower(name)) WHERE status = 'running', made in the initial migration.</summary>
    const string RunningNameIndex = "ux_strategies_user_id_running_name";

    public static void MapStrategies(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard", async (AppDbContext db, Sessions sessions, AiRounds rounds, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            var all = await db.Strategies.AsNoTracking().Where(strategy => strategy.UserId == user.Id).Include(strategy => strategy.Assets).ToListAsync(cancellationToken);
            // Running ones newest first, then the archived ones, last archived first.
            var strategies = all.Where(s => s.Status == StrategyStatus.Running)
                .OrderByDescending(s => s.StartedOn).ThenByDescending(s => s.CreatedAt)
                .Concat(all.Where(s => s.Status != StrategyStatus.Running).OrderByDescending(s => s.EndedAt))
                .ToList();
            var running = strategies.Where(s => s.Status == StrategyStatus.Running).ToList();
            var today = Today(user, time);
            // AI rounds whose week ran out are skipped before anything is shown.
            await rounds.SweepAsync(running.Select(s => new AiStrategyDay(s, today)), cancellationToken);
            var book = await StrategyBook.LoadAsync(db, strategies, today, cancellationToken);
            // Only running strategies wait for the user: an ended one's signals are closed.
            var ids = running.Select(s => s.Id).ToList();
            // Live signals still waiting for an answer, newest first.
            var waiting = await db.Signals.AsNoTracking()
                .Where(signal => ids.Contains(signal.StrategyId) && signal.Status == SignalStatus.Waiting)
                .OrderByDescending(signal => signal.SignalDate).ThenByDescending(signal => signal.CreatedAt)
                .Join(db.Assets, signal => signal.AssetId, asset => asset.Id, (signal, asset) => new { signal.Id, signal.Side, signal.StrategyId, asset.Name })
                .ToListAsync(cancellationToken);
            var names = running.ToDictionary(s => s.Id, s => s.Name);
            // AI rounds due, first: a round has a week to be run, a signal waits longer.
            var due = await db.AiRounds.AsNoTracking()
                .Where(r => ids.Contains(r.StrategyId) && r.Status == AiRoundStatus.Due && r.DueOn <= today)
                .OrderByDescending(r => r.DueOn).Select(r => new { r.Id, r.StrategyId }).ToListAsync(cancellationToken);
            // Where each running strategy's stocks stand, as its page's pills say.
            var signals = (await db.Signals.AsNoTracking().Where(s => ids.Contains(s.StrategyId)).ToListAsync(cancellationToken))
                .ToLookup(s => s.StrategyId);
            var latest = (await db.DailyEvaluations.AsNoTracking().Where(r => ids.Contains(r.StrategyId))
                .GroupBy(r => new { r.StrategyId, r.AssetId }).Select(g => g.OrderByDescending(r => r.Date).First())
                .ToListAsync(cancellationToken)).ToDictionary(r => (r.StrategyId, r.AssetId));
            var aiScores = new Dictionary<Guid, Dictionary<Guid, decimal>>();
            foreach (var s in running.Where(AiRounds.IsAi)) aiScores[s.Id] = await rounds.LastScoresAsync(s.Id, cancellationToken);
            var counts = running.ToDictionary(s => s.Id, s => CountsOf(s, book, signals[s.Id], latest, aiScores.GetValueOrDefault(s.Id)));
            // The fund a fund template follows, named on its card.
            var fundIds = strategies.Where(s => TemplateCatalog.Find(s.TemplateKey)!.InvestsIn != InvestsIn.StockList)
                .SelectMany(s => Followed(s).Select(a => a.AssetId)).Distinct().ToList();
            var symbols = await db.Assets.AsNoTracking().Where(a => fundIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Symbol, cancellationToken);
            string? FundOf(Strategy s) => Followed(s).Select(a => symbols.GetValueOrDefault(a.AssetId)).FirstOrDefault(n => n is not null);
            return new Dashboard(today, [.. strategies.Select(strategy => Summary(strategy, user, (book, today)) with
                {
                    Stocks = counts.GetValueOrDefault(strategy.Id),
                    Fund = FundOf(strategy),
                })],
                [.. due.Select(r => new WaitingItem(r.Id, WaitingKind.AiRound, r.StrategyId, names[r.StrategyId], null)),
                 .. waiting.Select(w => new WaitingItem(w.Id, w.Side == TradeSide.Buy ? WaitingKind.BuySignal : WaitingKind.SellSignal,
                    w.StrategyId, names[w.StrategyId], w.Name))]);
        })
        .RequireUser()
        .WithTags("Dashboard")
        .WithName("GetDashboard")
        .WithSummary("The user's strategies, running ones first, with their results, and the live signals and AI rounds waiting for an answer.");

        app.MapGet("/strategies/{id:guid}", async Task<Results<Ok<StrategyDetail>, NotFound>> (
            Guid id, AppDbContext db, Sessions sessions, AiRounds rounds, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            var strategy = await db.Strategies.AsNoTracking().Include(s => s.Assets)
                .SingleOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            var today = Today(user, time);
            var ai = AiRounds.IsAi(strategy) ? await rounds.SummaryAsync(strategy, today, cancellationToken) : default;
            return TypedResults.Ok(await DetailAsync(db, strategy, user, today, cancellationToken, ai.Summary, ai.LastScores));
        })
        .RequireUser()
        .WithTags("Strategies")
        .WithName("GetStrategy")
        .WithSummary("The strategy page: results next to the rules' and the index fund's, the budget used, and where each stock stands.");

        var group = app.MapGroup("/strategies").WithTags("Strategies").RequireUser();

        group.MapGet("/{id:guid}/stocks/{assetId:guid}/chart", async Task<Results<Ok<StockChartData>, NotFound>> (
            Guid id, Guid assetId, AppDbContext db, Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            var strategy = await db.Strategies.AsNoTracking()
                .Where(s => s.Id == id && s.UserId == user.Id && s.Assets.Any(a => a.AssetId == assetId))
                .Select(s => new { s.TemplateKey, s.Params, s.Budget.MaxSteps }).SingleOrDefaultAsync(cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            var bars = await db.Prices.AsNoTracking().Where(p => p.AssetId == assetId).OrderBy(p => p.Date)
                .Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToListAsync(cancellationToken);
            if (bars.Count == 0) return TypedResults.Ok(new StockChartData(null, []));
            var from = bars[^1].Date.AddDays(-StockChart.Days);

            var startedOn = await db.DailyEvaluations.Where(e => e.StrategyId == id && e.AssetId == assetId)
                .MinAsync(e => (DateOnly?)e.Date, cancellationToken);
            var checks = TemplateRules.Supports(strategy.TemplateKey)
                ? StockChart.Checks(() => TemplateRules.Create(strategy.TemplateKey, bars, strategy.Params), bars, from, startedOn, strategy.MaxSteps ?? 1)
                : [];
            return TypedResults.Ok(new StockChartData(startedOn, [.. bars.Where(b => b.Date >= from)
                .Select(b => new ChartDay(b.Date, b.Close, checks.GetValueOrDefault(b.Date) ?? []))]));
        })
        .WithName("GetStockChart")
        .WithSummary("The stock page's chart: a stock's daily closes over the last 90 days with the rules' checks of each day, for a stock in one of the user's strategies.");

        group.MapPost("/", async Task<Results<Created<StrategySummary>, ValidationProblem, Conflict<string>, NotFound>> (
            NewStrategy request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(request.TemplateKey ?? "") is not { } template) return TypedResults.NotFound();

            var user = await CurrentUser(sessions, cancellationToken);
            var ids = request.AssetIds ?? [];
            var assets = await db.Assets.Where(asset => ids.Contains(asset.Id)).ToListAsync(cancellationToken);
            var errors = NewStrategyValidation.Validate(request, template, assets, user.Locale);
            // Live signals are sent by email (spec: Choose the mode), so a live strategy needs a confirmed address.
            if (request.Mode == StrategyMode.Live && user.EmailVerifiedAt is null) errors["mode"] = [Messages.ConfirmEmailToGoLive.In(user.Locale)];
            // "Go live with these settings": a live copy keeps the simulation it came from, which must be one of the user's.
            var source = request.Mode == StrategyMode.Live ? request.SourceStrategyId : null;
            if (source is { } sourceId && !await db.Strategies.AnyAsync(
                    s => s.Id == sourceId && s.UserId == user.Id && s.Mode == StrategyMode.Simulation, cancellationToken))
                errors["sourceStrategyId"] = [Messages.NotYourSimulation.In(user.Locale)];
            var parameters = TemplateSettings.Apply(template, request.Settings, (field, message) => errors[field] = [message], user.Locale);
            // The AI template's prompt choices (Review: "What the AI reads", Edit prompt).
            if (parameters is not null && template == TemplateCatalog.Ai)
                parameters = AiRoundEndpoints.Apply(parameters, request.Ai, (field, message) => errors[$"ai.{field}"] = [message], user.Locale);
            if (errors.Count > 0 || parameters is null) return TypedResults.ValidationProblem(errors);

            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM users WHERE id = {user.Id} FOR NO KEY UPDATE", cancellationToken);
            var running = await db.Strategies.CountAsync(s => s.UserId == user.Id && s.Status == StrategyStatus.Running, cancellationToken);
            if (running >= NewStrategyValidation.MaxRunningStrategies)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["strategies"] = [Messages.StrategyLimit.In(user.Locale, NewStrategyValidation.MaxRunningStrategies)]
                });

            var strategy = new Strategy
            {
                UserId = user.Id,
                TemplateKey = template.Key,
                DefaultsVersion = template.DefaultsVersion,
                Name = request.Name.Trim(),
                Mode = request.Mode!.Value,
                Status = StrategyStatus.Running,
                StartedOn = Today(user, time),
                Currency = request.Currency ?? user.Currency,
                Budget = request.Budget,
                Params = parameters,
                SourceStrategyId = source,
                Assets = [.. assets.Select(asset => new StrategyAsset
                    { AssetId = asset.Id, Active = true, AddedAt = time.GetUtcNow() })],
            };
            db.Strategies.Add(strategy);
            // Its first AI round is due now (spec: Confirm, "Your first AI round is due now").
            if (template == TemplateCatalog.Ai) db.AiRounds.Add(new AiRound { StrategyId = strategy.Id, DueOn = strategy.StartedOn, Status = AiRoundStatus.Due });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            // The running name index refuses a taken name, also when two requests race.
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: RunningNameIndex })
            {
                return TypedResults.Conflict(Messages.NameTaken.In(user.Locale));
            }
            return TypedResults.Created($"/strategies/{strategy.Id}", Summary(strategy, user));
        })
        .WithName("StartStrategy")
        .WithSummary("Starts a strategy from a template, in simulation or live, with the user's budget and stocks.");

        group.MapPut("/{id:guid}/settings", async Task<Results<NoContent, NotFound, ValidationProblem>> (
            Guid id, UpdateStrategySettings request, AppDbContext db, Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.Id == id && s.UserId == user.Id && s.Status == StrategyStatus.Running, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();

            if (TemplateCatalog.Find(strategy.TemplateKey) is not { } template) return TypedResults.NotFound();

            var errors = new Dictionary<string, string[]>();
            var parameters = TemplateSettings.Apply(template, request.Settings, (field, message) => errors[field] = [message], user.Locale, strategy.Params);
            if (errors.Count > 0 || parameters is null) return TypedResults.ValidationProblem(errors);

            strategy.Params = parameters;
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("UpdateStrategySettings")
        .WithSummary("Updates the settings (rules) of a running strategy.");

        group.MapPut("/{id:guid}/assets", async Task<Results<NoContent, NotFound, ValidationProblem, Conflict<string>>> (
            Guid id, UpdateStrategyAssets request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Locked first, so two saves of the list go one after the other and an archive meanwhile wins.
            var mine = await db.Database.SqlQuery<Guid>(
                    $"""SELECT id AS "Value" FROM strategies WHERE id = {id} AND user_id = {user.Id} FOR NO KEY UPDATE""")
                .ToListAsync(cancellationToken);
            if (mine.Count == 0) return TypedResults.NotFound();
            var strategy = await db.Strategies.Include(s => s.Assets).SingleAsync(s => s.Id == id, cancellationToken);
            if (strategy.Status != StrategyStatus.Running) return TypedResults.Conflict(Messages.StrategyIsEnded.In(user.Locale));
            if (TemplateCatalog.Find(strategy.TemplateKey) is not { } template) return TypedResults.NotFound();
            // A fund template's fund doesn't change while it runs (not designed): Edit list is for stock lists.
            if (template.Watches is DefaultFund) return TypedResults.Conflict(Messages.FundCantChange.In(user.Locale));

            var ids = request.AssetIds ?? [];
            var assets = await db.Assets.Where(asset => ids.Contains(asset.Id)).ToListAsync(cancellationToken);
            if (NewStrategyValidation.AssetsError(ids, template, assets, user.Locale,
                    [.. strategy.Assets.Where(a => a.Active && a.RemovedAt is null).Select(a => a.AssetId)]) is { } invalid)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["assetIds"] = [invalid] });

            var now = time.GetUtcNow();
            // Spec (strategy page, Edit list): an unticked stock stops its signals, and its trades stay. A stock ticked again
            // is checked from the day it comes back (no back-dating, #45): the daily check reads that day from AddedAt.
            var removed = new List<Guid>();
            foreach (var listed in strategy.Assets)
            {
                var ticked = ids.Contains(listed.AssetId);
                var following = listed.Active && listed.RemovedAt is null;
                if (ticked && !following)
                {
                    (listed.Active, listed.RemovedAt, listed.AddedAt) = (true, null, now);
                }
                else if (!ticked && following)
                {
                    (listed.Active, listed.RemovedAt) = (false, now);
                    removed.Add(listed.AssetId);
                }
            }
            foreach (var added in ids.Where(assetId => strategy.Assets.All(a => a.AssetId != assetId)).ToList())
                strategy.Assets.Add(new StrategyAsset { AssetId = added, Active = true, AddedAt = now });
            strategy.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);

            // Their waiting signals can't be answered in time any more: missed, ending today, as when archiving.
            var today = Today(user, time);
            if (removed.Count > 0)
                await db.Signals.Where(s => s.StrategyId == id && s.Status == SignalStatus.Waiting && removed.Contains(s.AssetId))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Missed)
                        .SetProperty(x => x.EndedOn, x => x.EndedOn ?? today)
                        .SetProperty(x => x.SnoozedUntil, (DateOnly?)null), cancellationToken);
            // A simulation's buy still waiting for its trade at the next open won't get one now (the daily check no longer visits the
            // stock): missed too, so it doesn't hold its step of the budget.
            if (removed.Count > 0 && strategy.Mode == StrategyMode.Simulation)
                await db.Signals.Where(s => s.StrategyId == id && s.Status == SignalStatus.Confirmed && removed.Contains(s.AssetId)
                        && !db.SignalTrades.Any(t => t.SignalId == s.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Missed)
                        .SetProperty(x => x.AnsweredAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.EndedOn, x => x.EndedOn ?? today), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("UpdateStrategyAssets")
        .WithSummary("Edit list: sets the stocks a running strategy follows. Unticked ones stop their signals (waiting ones become missed); their trades stay.");

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound, Conflict<string>>> (
            Guid id, AppDbContext db, Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            var status = await db.Strategies.Where(s => s.Id == id && s.UserId == user.Id).Select(s => (StrategyStatus?)s.Status)
                .SingleOrDefaultAsync(cancellationToken);
            if (status is null) return TypedResults.NotFound();
            // Only an archived one: a running strategy is archived first, so its signals close before it goes.
            if (status != StrategyStatus.Ended) return TypedResults.Conflict(Messages.ArchiveBeforeDelete.In(user.Locale));
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Its AI rounds first, keeping the ids of their shared copies: once the rounds are gone nothing points to those
            // (spec: AI data sharing). Deleting the rows waits for a round being used right now, and then sees its shared copy.
            var shared = await db.Database.SqlQuery<Guid>($"""
                WITH gone AS (
                    DELETE FROM ai_rounds r USING strategies s
                    WHERE r.strategy_id = s.id AND s.id = {id} AND s.user_id = {user.Id}
                    RETURNING r.shared_round_id)
                SELECT shared_round_id AS "Value" FROM gone WHERE shared_round_id IS NOT NULL
                """).ToListAsync(cancellationToken);
            if (shared.Count > 0) await db.SharedAiRounds.Where(s => shared.Contains(s.Id)).ExecuteDeleteAsync(cancellationToken);
            // Its stocks, signals, trades and evaluations go with it (ON DELETE CASCADE).
            var deleted = await db.Strategies.Where(s => s.Id == id && s.UserId == user.Id && s.Status == StrategyStatus.Ended)
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0) return TypedResults.NotFound();
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("DeleteStrategy")
        .WithSummary("Deletes an archived strategy for good, with its signals, trades, results and AI rounds.");

        group.MapPut("/{id:guid}/name", async Task<Results<NoContent, NotFound, ValidationProblem, Conflict<string>>> (
            Guid id, RenameStrategy request, AppDbContext db, Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            if (NewStrategyValidation.NameError(request.Name, user.Locale) is { } invalid)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["name"] = [invalid] });

            strategy.Name = request.Name!.Trim();
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            // As on Confirm: the running name index refuses a name another running strategy of the user has.
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: RunningNameIndex })
            {
                return TypedResults.Conflict(Messages.NameTaken.In(user.Locale));
            }
            return TypedResults.NoContent();
        })
        .WithName("RenameStrategy")
        .WithSummary("Renames a strategy, with Confirm's name rules (unique among the user's running strategies).");

        group.MapPost("/{id:guid}/archive", async Task<Results<NoContent, NotFound, Conflict<string>>> (
            Guid id, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            if (!await db.Strategies.AnyAsync(s => s.Id == id && s.UserId == user.Id, cancellationToken)) return TypedResults.NotFound();
            var now = time.GetUtcNow();
            var today = Today(user, time);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Only while it runs, so two taps archive it once. The daily check runs running strategies only, and the running name index frees its name.
            var ended = await db.Strategies.Where(s => s.Id == id && s.UserId == user.Id && s.Status == StrategyStatus.Running)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, StrategyStatus.Ended).SetProperty(x => x.EndedAt, now).SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
            if (ended == 0) return TypedResults.Conflict(Messages.StrategyIsEnded.In(user.Locale));
            // Its signals still waiting can't be answered in time any more: missed, ending today (one ended already keeps its day).
            // Only a waiting signal can be snoozed (the table's check).
            await db.Signals.Where(s => s.StrategyId == id && s.Status == SignalStatus.Waiting)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Missed)
                    .SetProperty(x => x.EndedOn, x => x.EndedOn ?? today)
                    .SetProperty(x => x.SnoozedUntil, (DateOnly?)null), cancellationToken);
            // Its AI round due is skipped, as one not run in time is; no new one comes (rounds are kept for running strategies only).
            await db.AiRounds.Where(r => r.StrategyId == id && r.Status == AiRoundStatus.Due)
                .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, AiRoundStatus.Skipped), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("ArchiveStrategy")
        .WithSummary("Archives a running strategy: the daily check stops, its waiting signals become missed and its name is free. Its history stays, read-only.");

    }

    /// <summary>The logged-in user: the endpoints require one (RequireUser), so it is there.</summary>
    internal static async Task<User> CurrentUser(Sessions sessions, CancellationToken cancellationToken) =>
        (await sessions.CurrentAsync(cancellationToken))!;

    /// <summary>The user's local date; UTC if their time zone isn't known to the server.</summary>
    internal static DateOnly Today(User user, TimeProvider time) => Today(user.TimeZone, time.GetUtcNow());

    /// <summary>The date in a time zone at <paramref name="now"/>; UTC if the server doesn't know the zone.</summary>
    internal static DateOnly Today(string timeZone, DateTimeOffset now)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }

    /// <summary>The user's local date the strategy ended; null while it runs.</summary>
    internal static DateOnly? EndedOn(Strategy strategy, User user) => strategy.EndedAt is { } at ? Today(user.TimeZone, at) : null;

    /// <summary>The day its results are worked out to: today, or an ended strategy's end date (its results are frozen there).</summary>
    static DateOnly ResultsDay(Strategy strategy, User user, DateOnly today) => EndedOn(strategy, user) is { } ended && ended < today ? ended : today;

    /// <summary>A strategy's card; without <paramref name="trades"/> (a new strategy) it has no results and nothing used.</summary>
    static StrategySummary Summary(Strategy strategy, User user, (StrategyBook Book, DateOnly Today)? trades = null)
    {
        var template = TemplateCatalog.Find(strategy.TemplateKey)!;
        var texts = TemplateTexts.For(template, user.Locale);
        var book = trades?.Book;
        var figures = trades is { } t ? t.Book.Figures(strategy, ResultsDay(strategy, user, t.Today)) : null;
        var results = figures is null ? null : new StrategyResults(figures.You, figures.Index, [.. figures.History.Select(p => p.You)]);
        return new StrategySummary(strategy.Id, strategy.Name, strategy.TemplateKey, texts.Name, strategy.Mode,
            strategy.StartedOn, strategy.Currency, strategy.Budget.Amount, book?.Used(strategy) ?? 0, results, PastResultOf(strategy, template, texts),
            strategy.Status, EndedOn(strategy, user), trades is { } d ? d.Book.IndexSinceStart(strategy, ResultsDay(strategy, user, d.Today)) : null);
    }

    static IEnumerable<StrategyAsset> Followed(Strategy strategy) => strategy.Assets.Where(a => a.Active && a.RemovedAt is null);

    /// <summary>A running strategy's followed stocks counted by pill, for its dashboard card.</summary>
    static StockCounts CountsOf(Strategy strategy, StrategyBook book, IEnumerable<Signal> signals,
        IReadOnlyDictionary<(Guid, Guid), DailyEvaluation> latest, IReadOnlyDictionary<Guid, decimal>? aiScores)
    {
        var you = book.You(strategy);
        var own = signals.ToList();
        var states = Followed(strategy).Select(a => StockStateOf(strategy, a.AssetId, false,
            latest.GetValueOrDefault((strategy.Id, a.AssetId)), [.. own.Where(s => s.AssetId == a.AssetId)], [.. you.Where(b => b.AssetId == a.AssetId)], aiScores))
            .ToList();
        // Bear markets' distance is above the low, not from a buy rule, so it is never "close".
        bool Close(StockState s) => s.Status is StockStatus.Watching or StockStatus.OutOfTheMarket && strategy.TemplateKey != "bear"
            && s.Distance is > 0 and <= StockCounts.ClosePct;
        return new StockCounts(
            states.Count(s => s.Status is StockStatus.WaitingForYou or StockStatus.Signal),
            states.Count(Close),
            states.Count(s => s.Status is StockStatus.Watching or StockStatus.OutOfTheMarket or StockStatus.NoBudget && !Close(s)),
            states.Count(s => s.Status is StockStatus.Holding or StockStatus.Building or StockStatus.Done),
            states.Count(s => s.Status == StockStatus.NotCandidate));
    }

    static PastResult? PastResultOf(Strategy strategy, Template template, TemplateTexts texts, IEnumerable<Guid>? activeAssetIds = null) =>
        template.Backtest?.LastOrDefault() is { } all && texts.Backtest is { } words
            ? new PastResult(words.Years, all.TemplatePerYear, all.IndexPerYear, all.WorstYear, all.IndexWorstYear,
                IsCustomized(strategy, template, activeAssetIds))
            : null;

    internal static bool IsCustomized(Strategy strategy, Template template, IEnumerable<Guid>? activeAssetIds = null)
    {
        if (template.Settings.Count > 0)
        {
            var root = strategy.Params.RootElement;
            var defaultRoot = template.ParamsJson().RootElement;
            foreach (var s in template.Settings)
            {
                if (root.TryGetProperty(s.Key, out var prop) && defaultRoot.TryGetProperty(s.Key, out var defProp)
                    && prop.ValueKind == JsonValueKind.Number && defProp.ValueKind == JsonValueKind.Number
                    && prop.GetDecimal() != defProp.GetDecimal())
                {
                    return true;
                }
            }
        }

        var activeIds = activeAssetIds ?? (strategy.Assets is null
            ? []
            : strategy.Assets.Where(a => a.Active && a.RemovedAt == null).Select(a => a.AssetId));
        return template.Watches switch
        {
            StarterList starter => activeIds.Any(id => !starter.Stocks.Any(stock => stock.Id == id)),
            DefaultFund defaultFund => activeIds.Any(id => id != defaultFund.Fund.Id),
            _ => false,
        };
    }

    static async Task<StrategyDetail> DetailAsync(AppDbContext db, Strategy strategy, User user, DateOnly today, CancellationToken cancellationToken,
        AiRoundsSummary? ai = null, IReadOnlyDictionary<Guid, decimal>? aiScores = null)
    {
        var template = TemplateCatalog.Find(strategy.TemplateKey)!;
        var texts = TemplateTexts.For(template, user.Locale);
        var book = await StrategyBook.LoadAsync(db, [strategy], today, cancellationToken);
        var live = strategy.Mode == StrategyMode.Live;
        var resultsDay = ResultsDay(strategy, user, today);

        var signalEntities = await db.Signals.AsNoTracking().Where(s => s.StrategyId == strategy.Id)
            .OrderByDescending(s => s.SignalDate).ThenByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var shown = strategy.Assets.Where(a => a.Active && a.RemovedAt is null).Select(a => a.AssetId).ToList();
        var you = book.You(strategy);
        // A stock unticked with Edit list keeps its row, dimmed, while it has trades in the results, the user's or (live) the rules' (spec: strategy page).
        var traded = you.Concat(book.Rules(strategy) ?? []).Select(t => t.AssetId).ToHashSet();
        var removed = strategy.Assets.Where(a => !shown.Contains(a.AssetId) && traded.Contains(a.AssetId)).Select(a => a.AssetId).ToList();

        // Assets referenced by the strategy, its signals, or its trades.
        var allAssetIds = shown.Concat(removed).Concat(signalEntities.Select(s => s.AssetId)).Distinct().ToList();
        var assetsById = await db.Assets.AsNoTracking().Include(a => a.Texts).Where(a => allAssetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Each stock's latest evaluation: where the rules stand today.
        var latest = await db.DailyEvaluations.AsNoTracking().Where(r => r.StrategyId == strategy.Id)
            .GroupBy(r => r.AssetId).Select(g => g.OrderByDescending(r => r.Date).First())
            .ToDictionaryAsync(r => r.AssetId, cancellationToken);

        var assets = shown.Concat(removed).Where(assetsById.ContainsKey).Select(id => assetsById[id]).ToList();

        // The close each latest evaluation looked at (one day for most strategies).
        var checkedDays = latest.Values.Select(r => r.Date).Distinct().ToList();
        var closes = await db.Prices.AsNoTracking().Where(p => shown.Contains(p.AssetId) && checkedDays.Contains(p.Date))
            .ToDictionaryAsync(p => (p.AssetId, p.Date), p => p.Close, cancellationToken);

        var maxSteps = strategy.Budget.MaxSteps;
        var stocks = assets.Select(asset =>
        {
            var evaluation = latest.GetValueOrDefault(asset.Id);
            var trades = you.Where(b => b.AssetId == asset.Id).ToList();
            var invested = Performance.Used(trades);
            var (status, rulesSteps, userSteps, distance, signal, checks) = StockStateOf(strategy, asset.Id, removed.Contains(asset.Id), evaluation,
                [.. signalEntities.Where(s => s.AssetId == asset.Id)], trades, aiScores);
            DateOnly? candidateSince = evaluation?.Facts.RootElement.TryGetProperty("candidateSince", out var cs) == true
                && cs.ValueKind == JsonValueKind.String && DateOnly.TryParse(cs.GetString(), out var parsedSince)
                ? parsedSince : checks.FirstOrDefault(c => c.Group == CheckGroup.Candidate && c.Met == true && c.Date is not null)?.Date;
            return new StockRow(asset.Id, asset.Symbol, asset.Name, status,
                Steps: userSteps,
                RulesSteps: live ? rulesSteps : null,
                maxSteps, distance, signal?.Id, signal?.StepNo,
                Invested: invested,
                Result: Performance.GainPct(trades, book.Prices, resultsDay, strategy.Currency),
                SignalSide: signal?.Side,
                AiScore: aiScores is not null && aiScores.TryGetValue(asset.Id, out var score) ? score : null)
            {
                Exchange = asset.Exchange,
                Currency = asset.Currency,
                What = asset.Texts.FirstOrDefault(t => t.Locale == user.Locale)?.What
                    ?? asset.Texts.FirstOrDefault(t => t.Locale == TemplateTexts.DefaultLocale)?.What,
                CheckedOn = evaluation?.Date,
                CandidateSince = candidateSince,
                Close = evaluation is not null && closes.TryGetValue((asset.Id, evaluation.Date), out var close) ? close : null,
                Checks = checks,
            };
        })
        .OrderBy(row => StrategyPage.Order(row.Status)).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

        var budget = strategy.Budget;
        var steps = budget.Step is { } step and > 0 ? (int?)(int)Math.Floor(budget.Amount / step) : null;
        var counts = live
            ? new SignalCounts(signalEntities.Count(s => s.Status == SignalStatus.Confirmed), signalEntities.Count(s => s.Status == SignalStatus.Skipped),
                signalEntities.Count(s => s.Status == SignalStatus.Missed))
            : null;
        var signalHistory = signalEntities.Select(s =>
        {
            var asset = assetsById.GetValueOrDefault(s.AssetId);
            var reason = asset is null ? "" : EmailTexts.FactSentence(SignalLine.From(s, strategy, asset));
            return new StrategySignalItem(s.Id, s.AssetId, asset?.Symbol ?? "", asset?.Name ?? "", s.SignalDate, s.Side,
                s.StepNo, s.MaxSteps, s.StepAmount, s.ClosePrice, asset?.Currency ?? strategy.Currency, reason, s.Status,
                s.SkipReason, s.SnoozedUntil, s.EndedOn, s.CreatedAt);
        }).ToList();

        List<StrategyTradeItem> tradeItems;
        if (live)
        {
            var userTrades = await db.UserTrades.AsNoTracking()
                .Where(t => t.StrategyId == strategy.Id && t.DeletedAt == null)
                .OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            // In case userTrades have assets not already in assetsById
            var missingAssetIds = userTrades.Select(t => t.AssetId).Where(id => !assetsById.ContainsKey(id)).Distinct().ToList();
            if (missingAssetIds.Count > 0)
            {
                var extra = await db.Assets.AsNoTracking().Where(a => missingAssetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
                foreach (var (k, v) in extra) assetsById[k] = v;
            }

            tradeItems = userTrades.Select(t =>
            {
                var asset = assetsById.GetValueOrDefault(t.AssetId);
                return new StrategyTradeItem(t.Id, t.AssetId, asset?.Symbol ?? "", asset?.Name ?? "", t.TradeDate, t.Side,
                    t.Price, asset?.Currency ?? strategy.Currency, t.EnteredAs, t.Amount, t.Shares, t.Fee, strategy.Currency,
                    t.FxRate, t.FxSource, t.Source, t.SignalId);
            }).ToList();
        }
        else
        {
            var simTrades = await db.SignalTrades.AsNoTracking()
                .Where(t => t.StrategyId == strategy.Id)
                .OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var missingAssetIds = simTrades.Select(t => t.AssetId).Where(id => !assetsById.ContainsKey(id)).Distinct().ToList();
            if (missingAssetIds.Count > 0)
            {
                var extra = await db.Assets.AsNoTracking().Where(a => missingAssetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
                foreach (var (k, v) in extra) assetsById[k] = v;
            }

            tradeItems = simTrades.Select(t =>
            {
                var asset = assetsById.GetValueOrDefault(t.AssetId);
                return new StrategyTradeItem(t.Id, t.AssetId, asset?.Symbol ?? "", asset?.Name ?? "", t.TradeDate, t.Side,
                    t.Price, asset?.Currency ?? strategy.Currency, EnteredAs.Amount, t.Amount, t.Shares, t.Fee, strategy.Currency,
                    t.FxRate, FxSource.Reference, TradeSource.Signal, t.SignalId);
            }).ToList();
        }

        var foreignCurrencies = assetsById.Values.Select(a => a.Currency).Where(c => c != strategy.Currency).Distinct().ToList();
        var ratesList = new List<CurrencyRates>();
        if (foreignCurrencies.Count > 0)
        {
            var earliestTrade = tradeItems.Count > 0 ? tradeItems.Min(t => t.TradeDate) : strategy.StartedOn;
            var fromDate = earliestTrade < strategy.StartedOn ? earliestTrade : strategy.StartedOn;
            var exchange = await ExchangeRates.LoadAsync(db, fromDate, cancellationToken);
            foreach (var curr in foreignCurrencies)
            {
                var days = new List<RateDay>();
                for (var day = fromDate.AddDays(-ExchangeRates.LookBackDays); day <= today; day = day.AddDays(1))
                {
                    if (exchange.StoredOn(strategy.Currency, day) && exchange.StoredOn(curr, day)
                        && day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                        && exchange.Between(strategy.Currency, curr, day) is { } rate)
                    {
                        days.Add(new RateDay(day, rate));
                    }
                }
                ratesList.Add(new CurrencyRates(curr, days));
            }
        }

        var root = strategy.Params.RootElement;
        var settings = new Dictionary<string, decimal>();
        foreach (var s in template.Settings)
        {
            if (root.TryGetProperty(s.Key, out var prop) && prop.ValueKind == JsonValueKind.Number)
                settings[s.Key] = prop.GetDecimal();
        }

        var aiSetup = template == TemplateCatalog.Ai && strategy.Params.Deserialize<AiParams>(JsonSerializerOptions.Web) is { } aiParams
            ? new AiSetup(aiParams.News, aiParams.Mood, aiParams.Figures, aiParams.Instructions)
            : null;
        var sourceName = strategy.SourceStrategyId is { } sourceId
            ? await db.Strategies.AsNoTracking().Where(s => s.Id == sourceId && s.UserId == user.Id).Select(s => s.Name).SingleOrDefaultAsync(cancellationToken)
            : null;

        return new StrategyDetail(strategy.Id, strategy.Name, strategy.TemplateKey, texts.Name, template.InvestsIn, strategy.Mode, strategy.Status,
            EndedOn(strategy, user), strategy.StartedOn, today, strategy.Currency,
            new BudgetUsed(budget.Amount, book.Used(strategy), budget.Step, budget.MaxSteps, steps is null ? null : you.Count(b => b.Side == TradeSide.Buy), steps),
            book.Figures(strategy, resultsDay), PastResultOf(strategy, template, texts, shown), counts, texts.StockStatus, stocks, tradeItems, signalHistory, ratesList, settings, ai,
            aiSetup, sourceName is null ? null : strategy.SourceStrategyId, sourceName) { CheckTexts = texts.Checks };
    }

    /// <summary>Where a stock stands today: its pill, the steps and signal behind it, and how far the price is from the rules.</summary>
    record StockState(StockStatus Status, int RulesSteps, int UserSteps, decimal? Distance, Signal? Signal, IReadOnlyList<RuleCheck> Checks);

    /// <summary>
    /// A stock's state from its latest evaluation, its signals and the trades the results count (spec: Strategy page). For an AI
    /// strategy, <paramref name="aiScores"/> holds the last round's scores (null for other templates).
    /// </summary>
    static StockState StockStateOf(Strategy strategy, Guid assetId, bool removed, DailyEvaluation? evaluation, IReadOnlyList<Signal> own,
        IReadOnlyList<Buy> trades, IReadOnlyDictionary<Guid, decimal>? aiScores)
    {
        var live = strategy.Mode == StrategyMode.Live;
        var maxSteps = strategy.Budget.MaxSteps;
        var waiting = own.Where(s => s.Status == SignalStatus.Waiting).OrderBy(s => s.SignalDate).FirstOrDefault();
        var todays = evaluation is null ? null : own.FirstOrDefault(s => s.SignalDate == evaluation.Date);
        var (doneSteps, cycleStart) = strategy.TemplateKey == "bear" ? StrategyPage.BearCycle(evaluation?.Facts.RootElement) : default;
        var buySignals = own.Count(s => s.Side == TradeSide.Buy && (cycleStart is null || s.SignalDate >= cycleStart));
        int rulesSteps;
        if (maxSteps is null)
        {
            var lastSignal = own.OrderBy(s => s.SignalDate).ThenBy(s => s.CreatedAt).LastOrDefault();
            rulesSteps = lastSignal?.Side == TradeSide.Buy ? 1 : 0;
        }
        else
        {
            rulesSteps = doneSteps is { } done ? Math.Min(done, buySignals) : buySignals;
        }
        var userSteps = maxSteps is null ? (Performance.Used(trades) > 0 ? 1 : 0)
            : strategy.TemplateKey == "bear" && !live ? rulesSteps
            : doneSteps == 0 ? 0
            : trades.Count(b => b.Side == TradeSide.Buy && (cycleStart is null || b.Date >= cycleStart));

        // Before an AI strategy's daily check has a verdict: watching once a round scored the stock, else not scored yet.
        var latestStatus = evaluation?.Status
            ?? (aiScores is null ? null : aiScores.ContainsKey(assetId) ? EvaluationStatus.Watching : EvaluationStatus.NotCandidate);
        var checks = evaluation is null ? [] : ChecksOf(evaluation.Facts);
        var status = removed ? StockStatus.Removed : StrategyPage.WithCandidacy(
            StrategyPage.Status(strategy.Mode, latestStatus, waiting is not null, todays is not null, rulesSteps, maxSteps, strategy.TemplateKey, userSteps), checks);
        var signal = status switch { StockStatus.WaitingForYou => waiting, StockStatus.Signal => todays, _ => null };
        var distanceKey = strategy.TemplateKey == "bear" && evaluation?.Status == EvaluationStatus.Watching ? "aboveLowPct" : "distancePct";
        decimal? distance = evaluation?.Facts.RootElement.TryGetProperty(distanceKey, out var d) == true && d.ValueKind == JsonValueKind.Number
            ? d.GetDecimal() : null;
        return new StockState(status, rulesSteps, userSteps, distance, signal, checks);
    }

    /// <summary>The rule checks a daily evaluation stored (none before they were stored).</summary>
    static IReadOnlyList<RuleCheck> ChecksOf(JsonDocument facts) =>
        facts.RootElement.ValueKind == JsonValueKind.Object && facts.RootElement.TryGetProperty(StrategyEvaluator.ChecksKey, out var checks)
            && checks.ValueKind == JsonValueKind.Array
            ? checks.Deserialize<List<RuleCheck>>(JsonSerializerOptions.Web) ?? []
            : [];
}
