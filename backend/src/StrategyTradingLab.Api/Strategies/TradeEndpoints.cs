using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Signals;

namespace StrategyTradingLab.Api.Strategies;

public static class TradeEndpoints
{
    public static void MapTrades(this IEndpointRouteBuilder app)
    {
        var strategiesGroup = app.MapGroup("/strategies").WithTags("Trades").RequireUser();

        strategiesGroup.MapPost("/{id:guid}/trades", async Task<Results<Created<StrategyTradeItem>, ValidationProblem, NotFound, Conflict<string>>> (
            Guid id, ManualTradeRequest request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            var strategy = await db.Strategies.Include(s => s.Assets).FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            if (strategy.Mode != StrategyMode.Live) return TypedResults.Conflict("Trades can only be logged for live strategies.");

            var activeAsset = strategy.Assets.FirstOrDefault(a => a.AssetId == request.AssetId && a.Active && a.RemovedAt == null);
            if (activeAsset is null)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["assetId"] = ["Pick one of the strategy's stocks."] });

            var asset = await db.Assets.FindAsync([request.AssetId], cancellationToken);
            if (asset is null)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["assetId"] = ["Pick one of the strategy's stocks."] });

            var tradeDate = request.Date;
            var minRatesDate = tradeDate.HasValue && tradeDate.Value < strategy.StartedOn ? tradeDate.Value : strategy.StartedOn;
            var rates = strategy.Currency == asset.Currency ? new ExchangeRates([]) : await ExchangeRates.LoadAsync(db, minRatesDate, cancellationToken);
            var signalRequest = new SignalTradeRequest(request.Date, request.Price, request.EnteredAs, request.Amount, request.Shares, request.Fee,
                request.FxSource, request.FxRate);
            var (trade, errors) = SignalRules.CheckTrade(signalRequest, minDate: null, StrategyEndpoints.Today(user, time),
                day => rates.Between(strategy.Currency, asset.Currency, day));
            if (trade is null) return TypedResults.ValidationProblem(errors);

            var userTrade = new UserTrade
            {
                StrategyId = strategy.Id,
                AssetId = asset.Id,
                TradeDate = trade.Date,
                Side = request.Side,
                Price = trade.Price,
                EnteredAs = trade.EnteredAs,
                Amount = trade.Amount,
                Shares = trade.Shares,
                Fee = trade.Fee,
                FxRate = trade.FxRate,
                FxSource = trade.FxSource,
                Source = TradeSource.Manual,
                SignalId = null,
            };
            db.UserTrades.Add(userTrade);
            await db.SaveChangesAsync(cancellationToken);

            var item = new StrategyTradeItem(userTrade.Id, asset.Id, asset.Symbol, asset.Name, userTrade.TradeDate, userTrade.Side, userTrade.Price,
                asset.Currency, userTrade.EnteredAs, userTrade.Amount, userTrade.Shares, userTrade.Fee, strategy.Currency, userTrade.FxRate,
                userTrade.FxSource, userTrade.Source, userTrade.SignalId);

            return TypedResults.Created($"/trades/{userTrade.Id}", item);
        })
        .WithName("LogManualTrade")
        .WithSummary("Log a trade by hand on one of the strategy's stocks (marked Manual).");

        var tradesGroup = app.MapGroup("/trades").WithTags("Trades").RequireUser();

        tradesGroup.MapPut("/{id:guid}", async Task<Results<NoContent, ValidationProblem, NotFound, Conflict<string>>> (
            Guid id, UpdateTradeRequest request, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            var trade = await db.UserTrades.FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null, cancellationToken);
            if (trade is null) return TypedResults.NotFound();

            var strategy = await db.Strategies.FirstOrDefaultAsync(s => s.Id == trade.StrategyId && s.UserId == user.Id, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            if (strategy.Mode != StrategyMode.Live) return TypedResults.Conflict("Only live trades can be edited.");

            var asset = await db.Assets.FindAsync([trade.AssetId], cancellationToken);
            if (asset is null) return TypedResults.NotFound();

            DateOnly? minDate;
            string minDateMessage;
            if (trade.Source == TradeSource.Signal && trade.SignalId is { } signalId)
            {
                var signal = await db.Signals.FindAsync([signalId], cancellationToken);
                minDate = signal?.SignalDate;
                minDateMessage = "Use a day on or after the signal.";
            }
            else
            {
                minDate = null;
                minDateMessage = "";
            }

            var tradeDate = request.Date;
            var minRatesDate = tradeDate.HasValue && tradeDate.Value < strategy.StartedOn ? tradeDate.Value : (minDate ?? strategy.StartedOn);
            var rates = strategy.Currency == asset.Currency ? new ExchangeRates([]) : await ExchangeRates.LoadAsync(db, minRatesDate, cancellationToken);
            var signalRequest = new SignalTradeRequest(request.Date, request.Price, request.EnteredAs, request.Amount, request.Shares, request.Fee,
                request.FxSource, request.FxRate);
            var (checkedTrade, errors) = SignalRules.CheckTrade(signalRequest, minDate, StrategyEndpoints.Today(user, time),
                day => rates.Between(strategy.Currency, asset.Currency, day), minDateMessage: minDateMessage);
            if (checkedTrade is null) return TypedResults.ValidationProblem(errors);

            trade.TradeDate = checkedTrade.Date;
            trade.Price = checkedTrade.Price;
            trade.EnteredAs = checkedTrade.EnteredAs;
            trade.Amount = checkedTrade.Amount;
            trade.Shares = checkedTrade.Shares;
            trade.Fee = checkedTrade.Fee;
            trade.FxRate = checkedTrade.FxRate;
            trade.FxSource = checkedTrade.FxSource;
            trade.UpdatedAt = time.GetUtcNow();

            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("UpdateTrade")
        .WithSummary("Edit a trade's date, price, amount/shares, fee, or exchange rate.");

        tradesGroup.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound, Conflict<string>>> (
            Guid id, AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            var trade = await db.UserTrades.FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null, cancellationToken);
            if (trade is null) return TypedResults.NotFound();

            var strategy = await db.Strategies.FirstOrDefaultAsync(s => s.Id == trade.StrategyId && s.UserId == user.Id, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            if (strategy.Mode != StrategyMode.Live) return TypedResults.Conflict("Only live trades can be deleted.");

            trade.DeletedAt = time.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("DeleteTrade")
        .WithSummary("Soft-delete a trade.");
    }
}
