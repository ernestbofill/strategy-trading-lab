using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Notifications;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

public class SummaryEndpointTests(AccountsApi factory) : IClassFixture<AccountsApi>
{
    [Fact]
    public void DailySummaryDto_deserializes_from_camel_case_json_with_string_enums()
    {
        var json = """
        {
            "date": "2026-10-23",
            "hasChanges": true,
            "waitingSignals": [
                {
                    "signalId": "11111111-1111-1111-1111-111111111111",
                    "strategyId": "22222222-2222-2222-2222-222222222222",
                    "strategyName": "Dip",
                    "stockName": "Apple",
                    "side": "buy",
                    "signalDate": "2026-10-23",
                    "stepNo": 1,
                    "maxSteps": 5,
                    "closePrice": 150.5
                }
            ],
            "waitingAiRounds": [
                {
                    "strategyId": "22222222-2222-2222-2222-222222222222",
                    "strategyName": "AI Strat"
                }
            ],
            "whatChanged": [
                {
                    "signalId": "11111111-1111-1111-1111-111111111111",
                    "strategyId": "22222222-2222-2222-2222-222222222222",
                    "strategyName": "Dip",
                    "mode": "live",
                    "stockName": "Apple",
                    "side": "buy",
                    "signalDate": "2026-10-23",
                    "closePrice": 150.5,
                    "stepNo": 1,
                    "maxSteps": 5,
                    "text": "Apple: rules met"
                }
            ],
            "stockChanges": [
                {
                    "strategyId": "22222222-2222-2222-2222-222222222222",
                    "assetId": "33333333-3333-3333-3333-333333333333",
                    "strategyName": "Dip",
                    "mode": "simulation",
                    "stockName": "Tesla",
                    "kind": "cameClose",
                    "distancePct": 2.5,
                    "text": "Tesla: close"
                }
            ],
            "strategies": [
                {
                    "strategyId": "22222222-2222-2222-2222-222222222222",
                    "strategyName": "Dip",
                    "mode": "live",
                    "startedOn": "2026-10-01",
                    "signalsCount": 3
                }
            ]
        }
        """;

        var dto = JsonSerializer.Deserialize<DailySummaryDto>(json, JsonSerializerOptions.Web);
        Assert.NotNull(dto);
        Assert.True(dto.HasChanges);
        Assert.Equal(TradeSide.Buy, dto.WaitingSignals[0].Side);
        Assert.Equal(StrategyMode.Live, dto.WhatChanged[0].Mode);
        Assert.Equal(TradeSide.Buy, dto.WhatChanged[0].Side);
        Assert.Equal(StrategyMode.Simulation, dto.StockChanges[0].Mode);
        Assert.Equal(StockChangeKind.CameClose, dto.StockChanges[0].Kind);
        Assert.Equal(StrategyMode.Live, dto.Strategies[0].Mode);

        var serialized = JsonSerializer.Serialize(dto, JsonSerializerOptions.Web);
        Assert.Contains("\"mode\":\"live\"", serialized);
        Assert.Contains("\"mode\":\"simulation\"", serialized);
        Assert.Contains("\"side\":\"buy\"", serialized);
        Assert.Contains("\"kind\":\"cameClose\"", serialized);
    }
    [NeonFact]
    public async Task The_summary_reflects_quiet_state_before_strategies_and_changes_when_active()
    {
        var (client, email) = await factory.SignedUpAsync();

        // 1. Quiet state with no strategies
        var quiet = await client.GetFromJsonAsync<DailySummaryDto>("/summary");
        Assert.NotNull(quiet);
        Assert.True(quiet.Date.Year >= 2026);
        Assert.False(quiet.HasChanges);
        Assert.Empty(quiet.WaitingSignals);
        Assert.Empty(quiet.WaitingAiRounds);
        Assert.Empty(quiet.WhatChanged);
        Assert.Empty(quiet.StockChanges);
        Assert.Empty(quiet.Strategies);

        // 2. Active state with strategies, signals, and evaluations
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset1 = new Asset { Symbol = $"S1{tag}", Exchange = "TEST", Name = "Asset One", Currency = "USD" };
        var asset2 = new Asset { Symbol = $"S2{tag}", Exchange = "TEST", Name = "Asset Two", Currency = "USD" };
        db.Assets.AddRange(asset1, asset2);
        await db.SaveChangesAsync();

        var sessionDate = UsSession.LastComplete(DateTimeOffset.UtcNow);

        var strategy = new Strategy
        {
            UserId = user.Id,
            TemplateKey = "dip",
            Name = $"Summary {tag}",
            Mode = StrategyMode.Live,
            Status = StrategyStatus.Running,
            StartedOn = sessionDate.AddMonths(-1),
            Currency = "EUR",
            Budget = new Budget(3000, 100, 5),
            Params = TemplateCatalog.Dip.ParamsJson(),
            Assets =
            [
                new StrategyAsset { AssetId = asset1.Id, Active = true, AddedAt = DateTimeOffset.UtcNow },
                new StrategyAsset { AssetId = asset2.Id, Active = true, AddedAt = DateTimeOffset.UtcNow },
            ],
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var waitingSignal = new Signal
        {
            StrategyId = strategy.Id,
            AssetId = asset1.Id,
            Side = TradeSide.Buy,
            StepNo = 1,
            MaxSteps = 5,
            StepAmount = 100,
            SignalDate = sessionDate,
            ClosePrice = 150m,
            Facts = JsonDocument.Parse("""{"belowPeakPct":8.2}"""),
            Status = SignalStatus.Waiting,
        };

        var changedSignal = new Signal
        {
            StrategyId = strategy.Id,
            AssetId = asset2.Id,
            Side = TradeSide.Buy,
            StepNo = 2,
            MaxSteps = 5,
            StepAmount = 100,
            SignalDate = sessionDate,
            ClosePrice = 145m,
            Facts = JsonDocument.Parse("""{"belowPeakPct":10.5}"""),
            Status = SignalStatus.Confirmed,
            AnsweredAt = DateTimeOffset.UtcNow,
        };
        db.Signals.AddRange(waitingSignal, changedSignal);

        db.DailyEvaluations.AddRange(
            new DailyEvaluation
            {
                StrategyId = strategy.Id,
                AssetId = asset1.Id,
                Date = sessionDate.AddDays(-1),
                Status = EvaluationStatus.NotCandidate,
                Facts = JsonDocument.Parse("{}"),
            },
            new DailyEvaluation
            {
                StrategyId = strategy.Id,
                AssetId = asset1.Id,
                Date = sessionDate,
                Status = EvaluationStatus.Watching,
                Facts = JsonDocument.Parse("{\"distancePct\":3.2}"),
            });
        await db.SaveChangesAsync();

        var summary = await client.GetFromJsonAsync<DailySummaryDto>("/summary");

        Assert.NotNull(summary);
        Assert.True(summary.HasChanges);
        Assert.Single(summary.WaitingSignals);
        Assert.Equal("Asset One", summary.WaitingSignals[0].StockName);
        Assert.Single(summary.WhatChanged);
        Assert.Equal("Asset Two", summary.WhatChanged[0].StockName);
        Assert.Single(summary.StockChanges);
        Assert.Equal("Asset One", summary.StockChanges[0].StockName);
        Assert.Single(summary.Strategies);
        Assert.Equal(strategy.Name, summary.Strategies[0].StrategyName);
        Assert.Equal(2, summary.Strategies[0].SignalsCount);
    }
}
