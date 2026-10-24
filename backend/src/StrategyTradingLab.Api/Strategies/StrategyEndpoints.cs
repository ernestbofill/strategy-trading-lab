using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

public static class StrategyEndpoints
{
    /// <summary>The unique index on (user_id, lower(name)) WHERE status = 'running', made in the initial migration.</summary>
    const string RunningNameIndex = "ux_strategies_user_id_running_name";

    public static void MapStrategies(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard", async (AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            var strategies = await db.Strategies
                .Where(strategy => strategy.UserId == user.Id && strategy.Status == StrategyStatus.Running)
                .OrderByDescending(strategy => strategy.StartedOn).ThenByDescending(strategy => strategy.CreatedAt)
                .ToListAsync(cancellationToken);
            var today = Today(user, time);
            var book = await StrategyBook.LoadAsync(db, strategies, today, cancellationToken);
            var ids = strategies.Select(s => s.Id).ToList();
            // Live signals still waiting for an answer, newest first.
            var waiting = await db.Signals.AsNoTracking()
                .Where(signal => ids.Contains(signal.StrategyId) && signal.Status == SignalStatus.Waiting)
                .OrderByDescending(signal => signal.SignalDate).ThenByDescending(signal => signal.CreatedAt)
                .Join(db.Assets, signal => signal.AssetId, asset => asset.Id, (signal, asset) => new { signal.Id, signal.Side, signal.StrategyId, asset.Name })
                .ToListAsync(cancellationToken);
            var names = strategies.ToDictionary(s => s.Id, s => s.Name);
            return new Dashboard(today, [.. strategies.Select(strategy => Summary(strategy, user.Locale, book, today))],
                [.. waiting.Select(w => new WaitingItem(w.Id, w.Side == TradeSide.Buy ? WaitingKind.BuySignal : WaitingKind.SellSignal,
                    w.StrategyId, names[w.StrategyId], w.Name))]);
        })
        .RequireUser()
        .WithTags("Dashboard")
        .WithName("GetDashboard")
        .WithSummary("The user's running strategies, with their results, and the live signals waiting for an answer.");

        app.MapGet("/strategies/{id:guid}", async Task<Results<Ok<StrategyDetail>, NotFound>> (
            Guid id, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(sessions, cancellationToken);
            var strategy = await db.Strategies.AsNoTracking().Include(s => s.Assets)
                .SingleOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            return TypedResults.Ok(await DetailAsync(db, strategy, user, Today(user, time), cancellationToken));
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

            var ids = request.AssetIds ?? [];
            var assets = await db.Assets.Where(asset => ids.Contains(asset.Id)).ToListAsync(cancellationToken);
            var errors = NewStrategyValidation.Validate(request, template, assets);
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var user = await CurrentUser(sessions, cancellationToken);
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
                Params = template.ParamsJson(),
                Assets = [.. assets.Select(asset => new StrategyAsset
                    { AssetId = asset.Id, Active = true, AddedAt = time.GetUtcNow() })],
            };
            db.Strategies.Add(strategy);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            // The running name index refuses a taken name, also when two requests race.
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: RunningNameIndex })
            {
                return TypedResults.Conflict("You already have a running strategy with that name.");
            }
            return TypedResults.Created($"/strategies/{strategy.Id}", Summary(strategy, user.Locale));
        })
        .WithName("StartStrategy")
        .WithSummary("Starts a strategy from a template, in simulation or live, with the user's budget and stocks.");
    }

    /// <summary>The logged-in user: the endpoints require one (RequireUser), so it is there.</summary>
    static async Task<User> CurrentUser(Sessions sessions, CancellationToken cancellationToken) =>
        (await sessions.CurrentAsync(cancellationToken))!;

    /// <summary>The user's local date; UTC if their time zone isn't known to the server.</summary>
    static DateOnly Today(User user, TimeProvider time)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
    }

    static StrategySummary Summary(Strategy strategy, string locale, StrategyBook? book = null, DateOnly? today = null)
    {
        var template = TemplateCatalog.Find(strategy.TemplateKey)!;
        var texts = TemplateTexts.For(template, locale);
        var figures = book is null ? null : book.Figures(strategy, today!.Value);
        var results = figures is null ? null : new StrategyResults(figures.You, figures.Index, [.. figures.History.Select(p => p.You)]);
        return new StrategySummary(strategy.Id, strategy.Name, strategy.TemplateKey, texts.Name, strategy.Mode,
            strategy.StartedOn, strategy.Currency, strategy.Budget.Amount, book?.Used(strategy) ?? 0, results, PastResultOf(template, texts));
    }

    static PastResult? PastResultOf(Template template, TemplateTexts texts) =>
        template.Backtest?.LastOrDefault() is { } all && texts.Backtest is { } words
            ? new PastResult(words.Years, all.Template, all.Index, all.WorstYear)
            : null;

    static async Task<StrategyDetail> DetailAsync(AppDbContext db, Strategy strategy, User user, DateOnly today, CancellationToken cancellationToken)
    {
        var template = TemplateCatalog.Find(strategy.TemplateKey)!;
        var texts = TemplateTexts.For(template, user.Locale);
        var book = await StrategyBook.LoadAsync(db, [strategy], today, cancellationToken);
        var live = strategy.Mode == StrategyMode.Live;

        var signals = await db.Signals.AsNoTracking().Where(s => s.StrategyId == strategy.Id && s.Side == TradeSide.Buy)
            .Select(s => new { s.Id, s.AssetId, s.Status, s.StepNo, s.SignalDate, s.CreatedAt }).ToListAsync(cancellationToken);
        // Each stock's latest evaluation: where the rules stand today.
        var latest = await db.DailyEvaluations.AsNoTracking().Where(r => r.StrategyId == strategy.Id)
            .GroupBy(r => r.AssetId).Select(g => g.OrderByDescending(r => r.Date).First())
            .ToDictionaryAsync(r => r.AssetId, cancellationToken);
        var shown = strategy.Assets.Where(a => a.Active && a.RemovedAt is null).Select(a => a.AssetId).ToList();
        var assets = await db.Assets.AsNoTracking().Where(a => shown.Contains(a.Id)).ToListAsync(cancellationToken);

        var you = book.You(strategy);
        var maxSteps = strategy.Budget.MaxSteps;
        var stocks = assets.Select(asset =>
        {
            var own = signals.Where(s => s.AssetId == asset.Id).ToList();
            var evaluation = latest.GetValueOrDefault(asset.Id);
            var waiting = own.Where(s => s.Status == SignalStatus.Waiting).OrderBy(s => s.SignalDate).FirstOrDefault();
            var todays = evaluation is null ? null : own.FirstOrDefault(s => s.SignalDate == evaluation.Date);
            var buys = you.Where(b => b.AssetId == asset.Id).ToList();
            var status = StrategyPage.Status(strategy.Mode, evaluation?.Status, waiting is not null, todays is not null, own.Count, maxSteps);
            var signal = status switch { StockStatus.WaitingForYou => waiting, StockStatus.Signal => todays, _ => null };
            decimal? distance = evaluation?.Facts.RootElement.TryGetProperty("distancePct", out var d) == true && d.TryGetDecimal(out var pct) ? pct : null;
            return new StockRow(asset.Id, asset.Symbol, asset.Name, status,
                Steps: buys.Count,
                RulesSteps: live ? own.Count : null,
                maxSteps, distance, signal?.Id, signal?.StepNo,
                Invested: StrategyBook.Spent(strategy, buys),
                Result: Performance.GainPct(buys, book.Prices, today, strategy.Currency));
        })
        .OrderBy(row => StrategyPage.Order(row.Status)).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

        var budget = strategy.Budget;
        var steps = budget.Step is { } step and > 0 ? (int?)(int)Math.Floor(budget.Amount / step) : null;
        var counts = live
            ? new SignalCounts(signals.Count(s => s.Status == SignalStatus.Confirmed), signals.Count(s => s.Status == SignalStatus.Skipped),
                signals.Count(s => s.Status == SignalStatus.Missed))
            : null;
        return new StrategyDetail(strategy.Id, strategy.Name, strategy.TemplateKey, texts.Name, strategy.Mode, strategy.StartedOn, today,
            strategy.Currency,
            new BudgetUsed(budget.Amount, book.Used(strategy), budget.Step, budget.MaxSteps, steps is null ? null : you.Count, steps),
            book.Figures(strategy, today), PastResultOf(template, texts), counts, texts.StockStatus, stocks);
    }
}
