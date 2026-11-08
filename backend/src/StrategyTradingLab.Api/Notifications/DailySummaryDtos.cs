using System.Text.Json;
using System.Text.Json.Serialization;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Notifications;

public class JsonCamelCaseStringEnumConverter : JsonStringEnumConverter
{
    public JsonCamelCaseStringEnumConverter() : base(JsonNamingPolicy.CamelCase) { }
}

public record DailySummaryDto(
    DateOnly Date,
    bool HasChanges,
    IReadOnlyList<SummaryWaitingSignal> WaitingSignals,
    IReadOnlyList<SummaryWaitingAiRound> WaitingAiRounds,
    IReadOnlyList<SummaryChangedSignal> WhatChanged,
    IReadOnlyList<SummaryStockChange> StockChanges,
    IReadOnlyList<SummaryStrategyScore> Strategies);

public record SummaryWaitingSignal(
    Guid SignalId,
    Guid StrategyId,
    string StrategyName,
    string StockName,
    [property: JsonConverter(typeof(JsonCamelCaseStringEnumConverter))] TradeSide Side,
    DateOnly SignalDate,
    int? StepNo,
    int? MaxSteps,
    decimal ClosePrice);

public record SummaryWaitingAiRound(
    Guid StrategyId,
    string StrategyName);

public record SummaryChangedSignal(
    Guid SignalId,
    Guid StrategyId,
    string StrategyName,
    [property: JsonConverter(typeof(JsonCamelCaseStringEnumConverter))] StrategyMode Mode,
    string StockName,
    [property: JsonConverter(typeof(JsonCamelCaseStringEnumConverter))] TradeSide Side,
    DateOnly SignalDate,
    decimal ClosePrice,
    int? StepNo,
    int? MaxSteps,
    string Text);

public record SummaryStockChange(
    Guid StrategyId,
    Guid AssetId,
    string StrategyName,
    [property: JsonConverter(typeof(JsonCamelCaseStringEnumConverter))] StrategyMode Mode,
    string StockName,
    [property: JsonConverter(typeof(JsonCamelCaseStringEnumConverter))] StockChangeKind Kind,
    decimal? DistancePct,
    string Text);

public record SummaryStrategyScore(
    Guid StrategyId,
    string StrategyName,
    [property: JsonConverter(typeof(JsonCamelCaseStringEnumConverter))] StrategyMode Mode,
    DateOnly StartedOn,
    int SignalsCount);
