using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Ai;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
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
            var strategies = await db.Strategies.AsNoTracking()
                .Where(strategy => strategy.UserId == user.Id && strategy.Status == StrategyStatus.Running)
                .OrderByDescending(strategy => strategy.StartedOn).ThenByDescending(strategy => strategy.CreatedAt)
                .ToListAsync(cancellationToken);
            var today = Today(user, time);
            // AI rounds whose week ran out are skipped before anything is shown.
            await rounds.SweepAsync(strategies.Select(s => new AiStrategyDay(s, today)), cancellationToken);
            var book = await StrategyBook.LoadAsync(db, strategies, today, cancellationToken);
            var ids = strategies.Select(s => s.Id).ToList();
            // Live signals still waiting for an answer, newest first.
            var waiting = await db.Signals.AsNoTracking()
                .Where(signal => ids.Contains(signal.StrategyId) && signal.Status == SignalStatus.Waiting)
                .OrderByDescending(signal => signal.SignalDate).ThenByDescending(signal => signal.CreatedAt)
                .Join(db.Assets, signal => signal.AssetId, asset => asset.Id, (signal, asset) => new { signal.Id, signal.Side, signal.StrategyId, asset.Name })
                .ToListAsync(cancellationToken);
            var names = strategies.ToDictionary(s => s.Id, s => s.Name);
            // AI rounds due, first: a round has a week to be run, a signal waits longer.
            var due = await db.AiRounds.AsNoTracking()
                .Where(r => ids.Contains(r.StrategyId) && r.Status == AiRoundStatus.Due && r.DueOn <= today)
                .OrderByDescending(r => r.DueOn).Select(r => new { r.Id, r.StrategyId }).ToListAsync(cancellationToken);
            return new Dashboard(today, [.. strategies.Select(strategy => Summary(strategy, user.Locale, (book, today)))],
                [.. due.Select(r => new WaitingItem(r.Id, WaitingKind.AiRound, r.StrategyId, names[r.StrategyId], null)),
                 .. waiting.Select(w => new WaitingItem(w.Id, w.Side == TradeSide.Buy ? WaitingKind.BuySignal : WaitingKind.SellSignal,
                    w.StrategyId, names[w.StrategyId], w.Name))]);
        })
        .RequireUser()
        .WithTags("Dashboard")
        .WithName("GetDashboard")
        .WithSummary("The user's running strategies, with their results, and the live signals waiting for an answer.");

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

        group.MapPost("/", async Task<Results<Created<StrategySummary>, ValidationProblem, Conflict<string>, NotFound>> (
            NewStrategy request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(request.TemplateKey ?? "") is not { } template) return TypedResults.NotFound();

            var user = await CurrentUser(sessions, cancellationToken);
            var ids = request.AssetIds ?? [];
            var assets = await db.Assets.Where(asset => ids.Contains(asset.Id)).ToListAsync(cancellationToken);
            var errors = NewStrategyValidation.Validate(request, template, assets, user.Locale);
            var parameters = TemplateSettings.Apply(template, request.Settings, (field, message) => errors[field] = [message], user.Locale);
            // The AI template's prompt choices (Review: "What the AI reads", Edit prompt).
            if (parameters is not null && template == TemplateCatalog.Ai)
                parameters = AiRoundEndpoints.Apply(parameters, request.Ai, (field, message) => errors[$"ai.{field}"] = [message], user.Locale);
            if (errors.Count > 0 || parameters is null) return TypedResults.ValidationProblem(errors);

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
                Assets = [.. assets.Select(asset => new StrategyAsset
                    { AssetId = asset.Id, Active = true, AddedAt = time.GetUtcNow() })],
            };
            db.Strategies.Add(strategy);
            // Its first AI round is due now (spec: Confirm, "Your first AI round is due now").
            if (template == TemplateCatalog.Ai) db.AiRounds.Add(new AiRound { StrategyId = strategy.Id, DueOn = strategy.StartedOn, Status = AiRoundStatus.Due });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            // The running name index refuses a taken name, also when two requests race.
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: RunningNameIndex })
            {
                return TypedResults.Conflict(Messages.NameTaken.In(user.Locale));
            }
            return TypedResults.Created($"/strategies/{strategy.Id}", Summary(strategy, user.Locale));
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

    /// <summary>A strategy's card; without <paramref name="trades"/> (a new strategy) it has no results and nothing used.</summary>
    static StrategySummary Summary(Strategy strategy, string locale, (StrategyBook Book, DateOnly Today)? trades = null)
    {
        var template = TemplateCatalog.Find(strategy.TemplateKey)!;
        var texts = TemplateTexts.For(template, locale);
        var book = trades?.Book;
        var figures = trades is { } t ? t.Book.Figures(strategy, t.Today) : null;
        var results = figures is null ? null : new StrategyResults(figures.You, figures.Index, [.. figures.History.Select(p => p.You)]);
        return new StrategySummary(strategy.Id, strategy.Name, strategy.TemplateKey, texts.Name, strategy.Mode,
            strategy.StartedOn, strategy.Currency, strategy.Budget.Amount, book?.Used(strategy) ?? 0, results, PastResultOf(template, texts));
    }

    static PastResult? PastResultOf(Template template, TemplateTexts texts) =>
        template.Backtest?.LastOrDefault() is { } all && texts.Backtest is { } words
            ? new PastResult(words.Years, all.Template, all.Index, all.WorstYear)
            : null;

    static async Task<StrategyDetail> DetailAsync(AppDbContext db, Strategy strategy, User user, DateOnly today, CancellationToken cancellationToken,
        AiRoundsSummary? ai = null, IReadOnlyDictionary<Guid, decimal>? aiScores = null)
    {
        var template = TemplateCatalog.Find(strategy.TemplateKey)!;
        var texts = TemplateTexts.For(template, user.Locale);
        var book = await StrategyBook.LoadAsync(db, [strategy], today, cancellationToken);
        var live = strategy.Mode == StrategyMode.Live;

        var signalEntities = await db.Signals.AsNoTracking().Where(s => s.StrategyId == strategy.Id)
            .OrderByDescending(s => s.SignalDate).ThenByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var shown = strategy.Assets.Where(a => a.Active && a.RemovedAt is null).Select(a => a.AssetId).ToList();

        // Assets referenced by the strategy, its signals, or its trades.
        var allAssetIds = shown.Concat(signalEntities.Select(s => s.AssetId)).Distinct().ToList();
        var assetsById = await db.Assets.AsNoTracking().Where(a => allAssetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Each stock's latest evaluation: where the rules stand today.
        var latest = await db.DailyEvaluations.AsNoTracking().Where(r => r.StrategyId == strategy.Id)
            .GroupBy(r => r.AssetId).Select(g => g.OrderByDescending(r => r.Date).First())
            .ToDictionaryAsync(r => r.AssetId, cancellationToken);

        var assets = shown.Where(assetsById.ContainsKey).Select(id => assetsById[id]).ToList();

        var you = book.You(strategy);
        var maxSteps = strategy.Budget.MaxSteps;
        var stocks = assets.Select(asset =>
        {
            var own = signalEntities.Where(s => s.AssetId == asset.Id).ToList();
            var evaluation = latest.GetValueOrDefault(asset.Id);
            var waiting = own.Where(s => s.Status == SignalStatus.Waiting).OrderBy(s => s.SignalDate).FirstOrDefault();
            var todays = evaluation is null ? null : own.FirstOrDefault(s => s.SignalDate == evaluation.Date);
            var trades = you.Where(b => b.AssetId == asset.Id).ToList();
            var invested = Performance.Used(trades);
            var buySignals = own.Count(s => s.Side == TradeSide.Buy);
            int rulesSteps;
            if (maxSteps is null)
            {
                var lastSignal = own.OrderBy(s => s.SignalDate).ThenBy(s => s.CreatedAt).LastOrDefault();
                rulesSteps = lastSignal?.Side == TradeSide.Buy ? 1 : 0;
            }
            else if (strategy.TemplateKey == "bear"
                && evaluation?.Facts.RootElement.TryGetProperty("doneSteps", out var ds) == true
                && ds.ValueKind == JsonValueKind.Number)
            {
                rulesSteps = Math.Min(ds.GetInt32(), buySignals);
            }
            else
            {
                rulesSteps = buySignals;
            }
            var userSteps = maxSteps is null ? (invested > 0 ? 1 : 0)
                : strategy.TemplateKey == "bear" && !live ? rulesSteps
                : trades.Count(b => b.Side == TradeSide.Buy);

            // Before an AI strategy's daily check has a verdict: watching once a round scored the stock, else not scored yet.
            var latestStatus = evaluation?.Status
                ?? (ai is null ? null : aiScores!.ContainsKey(asset.Id) ? EvaluationStatus.Watching : EvaluationStatus.NotCandidate);
            var status = StrategyPage.Status(strategy.Mode, latestStatus, waiting is not null, todays is not null, rulesSteps, maxSteps, strategy.TemplateKey);
            var signal = status switch { StockStatus.WaitingForYou => waiting, StockStatus.Signal => todays, _ => null };
            var distanceKey = strategy.TemplateKey == "bear" && evaluation?.Status == EvaluationStatus.Watching ? "aboveLowPct" : "distancePct";
            decimal? distance = evaluation?.Facts.RootElement.TryGetProperty(distanceKey, out var d) == true && d.ValueKind == JsonValueKind.Number
                ? d.GetDecimal() : null;
            return new StockRow(asset.Id, asset.Symbol, asset.Name, status,
                Steps: userSteps,
                RulesSteps: live ? rulesSteps : null,
                maxSteps, distance, signal?.Id, signal?.StepNo,
                Invested: invested,
                Result: Performance.GainPct(trades, book.Prices, today, strategy.Currency),
                SignalSide: signal?.Side,
                AiScore: aiScores is not null && aiScores.TryGetValue(asset.Id, out var score) ? score : null);
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

        return new StrategyDetail(strategy.Id, strategy.Name, strategy.TemplateKey, texts.Name, strategy.Mode, strategy.StartedOn, today,
            strategy.Currency,
            new BudgetUsed(budget.Amount, book.Used(strategy), budget.Step, budget.MaxSteps, steps is null ? null : you.Count(b => b.Side == TradeSide.Buy), steps),
            book.Figures(strategy, today), PastResultOf(template, texts), counts, texts.StockStatus, stocks, tradeItems, signalHistory, ratesList, settings, ai);
    }
}
