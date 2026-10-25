using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Ai;

// The DTOs of the AI round screens (frontend/src/api/types.ts; camelCase JSON, enums as camelCase strings).

/// <summary>What the prompt asks for: the sources on, and the user's own instructions (null for the suggested ones).</summary>
public record AiSetup(bool News, bool Mood, bool Figures, string? Instructions);

/// <summary>The prompt for the review and the prompt editor, before a strategy exists. <c>AssetIds</c>: the ticked stocks.</summary>
public record AiPromptRequest(bool News, bool Mood, bool Figures, string? Instructions, int? EveryWeeks, IReadOnlyList<Guid>? AssetIds);

/// <summary>The whole prompt, and the suggested instructions for these sources (what "Reset to suggested" puts back).</summary>
public record AiPromptPreview(int Version, string Text, string Suggested);

/// <summary>
/// The AI round screen: the strategy's round that is due (or the next one), the prompt filled in for today, and what the screen
/// remembers from the last round. <c>Open</c> is whether it can be run today; <c>LastDay</c> the last day it can.
/// <c>SharingAsked</c> is whether the user has answered the sharing question (the consent box shows until then).
/// </summary>
public record AiRoundDetail(
    Guid StrategyId,
    string StrategyName,
    string TemplateName,
    StrategyMode Mode,
    string Currency,
    Guid RoundId,
    DateOnly DueOn,
    DateOnly LastDay,
    bool Open,
    DateOnly Today,
    int PromptVersion,
    bool PromptEdited,
    string Prompt,
    AiSetup Setup,
    string Suggested,
    IReadOnlyList<AiStockDto> Stocks,
    int EveryWeeks,
    int TopN,
    string? LastModel,
    bool Sharing,
    bool SharingAsked);

public record AiStockDto(Guid AssetId, string Symbol, string Name);

/// <summary>The pasted answer, to read or to use.</summary>
public record AiAnswerRequest(string? Answer);

/// <summary>
/// "Use these scores": the answer, the AI model, the sharing choice (null keeps the user's current one), and the prompt as the
/// screen showed it, so the round keeps the words the AI was given (refused if the prompt changed since).
/// </summary>
public record AiRoundSubmit(string? Answer, string? AiModel, bool? Share, string? Prompt = null);

/// <summary>A stock's scores as read, with the step it would get (or got).</summary>
public record AiScoreDto(Guid AssetId, string Symbol, string Name, decimal Overall, decimal? News, decimal? Mood, decimal? Figures,
    string Reason, int? Step);

/// <summary>
/// What was read from the answer: the scores (in the prompt's order), the problems that stop them from being used, the steps
/// they give, and why there are fewer than the top N (<c>Short</c>).
/// </summary>
public record AiReadingDto(IReadOnlyList<AiScoreDto> Scores, IReadOnlyList<string> Problems, bool Ok, int? MaxSteps, decimal? StepAmount,
    AiNoStep? Short);

/// <summary>The round is done: its signals, the first of which live opens on Confirm a signal.</summary>
public record AiRoundResult(Guid RoundId, IReadOnlyList<Guid> SignalIds, StrategyMode Mode);

/// <summary>A strategy's AI rounds, newest first, and how the picks did against the rest since each round.</summary>
public record AiRoundHistory(Guid StrategyId, string StrategyName, StrategyMode Mode, DateOnly Today, IReadOnlyList<AiRoundItem> Rounds, PicksVsRest? PicksVsRest);

/// <summary>
/// A round in the history. <c>Picks</c> and <c>Rest</c> are the average price change of the stocks that got a step and of the
/// others, since the round's session (null without prices).
/// </summary>
public record AiRoundItem(
    Guid Id,
    DateOnly DueOn,
    AiRoundStatus Status,
    DateTimeOffset? DoneAt,
    DateOnly? SessionDate,
    string? AiModel,
    int? PromptVersion,
    bool PromptEdited,
    string? Prompt,
    string? Answer,
    IReadOnlyList<AiScoreDto> Scores,
    decimal? Picks,
    decimal? Rest);

/// <summary>The picks' and the rest's average price change since their rounds, over every done round with prices for both.</summary>
public record PicksVsRest(decimal Picks, decimal Rest, int Rounds);

/// <summary>On the strategy page: the next round (due now or later) and the picks against the rest.</summary>
public record AiRoundsSummary(DateOnly? NextDueOn, bool Due, int Done, int Skipped, PicksVsRest? PicksVsRest);

/// <summary>Turns AI data sharing on or off.</summary>
public record AiSharingRequest(bool? Enabled);
