using System.Text.Json;

namespace StrategyTradingLab.Api.Data.Entities;

// One class per table in backend/DATA-MODEL.md. Column notes live there.

/// <summary>Tables with an updated_at column, stamped by UpdatedAtInterceptor.</summary>
public interface IHasUpdatedAt
{
    DateTimeOffset UpdatedAt { get; set; }
}

public class Asset
{
    public Guid Id { get; set; }
    public required string Symbol { get; set; }
    public required string Exchange { get; set; }
    public required string Name { get; set; }
    public AssetKind Kind { get; set; }
    public required string Currency { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>A stock's sector and industry, as the price source writes them. Only for added assets; the seeded ones have written lines.</summary>
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    /// <summary>Who added it at run time; null for a seeded asset (or once that account is deleted).</summary>
    public Guid? AddedBy { get; set; }
    /// <summary>Why the last price fetch couldn't be used (Yahoo's error, bars that aren't daily); null when it worked or before the first one.</summary>
    public string? PricesProblem { get; set; }
    /// <summary>When its prices were last fetched successfully; null before the first such fetch.</summary>
    public DateTimeOffset? PricesCheckedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<AssetText> Texts { get; set; } = [];
}

public class AssetText
{
    public Guid AssetId { get; set; }
    public required string Locale { get; set; }
    public required string What { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class User : IHasUpdatedAt
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    /// <summary>Asked at sign-up since invites; accounts from before have none until they add it on the account page.</summary>
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PasswordHash { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public required string TimeZone { get; set; }
    public required string Locale { get; set; }
    public string Currency { get; set; } = "EUR";
    public UserRole Role { get; set; } = UserRole.User;
    public bool OverviewEmail { get; set; } = true;
    public DateTimeOffset? DisclaimerAcceptedAt { get; set; }
    /// <summary>Share AI rounds anonymously (off by default), and when that last changed.</summary>
    public bool AiSharing { get; set; }
    public DateTimeOffset? AiSharingChangedAt { get; set; }
    /// <summary>The invite the account signed up with, if any: who invited it is the invite's maker.</summary>
    public Guid? InviteId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// An invite to sign up (spec: Sign-up, Invites): a code the invitee opens as a link or types, who made it, the role it
/// gives and whom it is for. The code is kept as it is, so its maker can copy the message again.
/// </summary>
public class Invite
{
    public Guid Id { get; set; }
    /// <summary>"ABCD-EFGH", see Auth/InviteCodes.</summary>
    public required string Code { get; set; }
    /// <summary>Null once its maker's account is deleted: the invite (and who joined with it) stays.</summary>
    public Guid? CreatedBy { get; set; }
    public UserRole Role { get; set; } = UserRole.Vip;
    public required string FirstName { get; set; }
    public string? LastName { get; set; }
    /// <summary>Where it was emailed, if it was; the invitee may sign up with another address.</summary>
    public string? Email { get; set; }
    /// <summary>The language of the message and the email, and the sign-up form's first language.</summary>
    public required string Locale { get; set; }
    public int MaxUses { get; set; } = 1;
    public int Uses { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

/// <summary>A logged-in device: the hash of the token in its stl_session cookie. See backend/AUTH.md.</summary>
public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required byte[] TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>A single-use link sent by email: verify the address, or reset the password. Only the token's hash is kept.</summary>
public class EmailToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public EmailTokenPurpose Purpose { get; set; }
    public required byte[] TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

public class PushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public DateTimeOffset? LastFailedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>budget_amount, budget_step and budget_max_steps. Step and max steps are null for all in, all out.</summary>
public record Budget(decimal Amount, decimal? Step, int? MaxSteps);

public class Strategy : IHasUpdatedAt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string TemplateKey { get; set; }
    public int DefaultsVersion { get; set; }
    public required string Name { get; set; }
    public StrategyMode Mode { get; set; }
    public StrategyStatus Status { get; set; }
    public DateOnly StartedOn { get; set; }
    /// <summary>When the user archived it (status <c>Ended</c>): it stops, and shows in the dashboard's collapsed Archived section.</summary>
    public DateTimeOffset? EndedAt { get; set; }
    public bool Notifications { get; set; } = true;
    public required string Currency { get; set; }
    public required Budget Budget { get; set; }
    public required JsonDocument Params { get; set; }
    public Guid? SourceStrategyId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<StrategyAsset> Assets { get; set; } = [];
}

public class StrategyAsset
{
    public Guid StrategyId { get; set; }
    public Guid AssetId { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class Signal
{
    public Guid Id { get; set; }
    public Guid StrategyId { get; set; }
    public Guid AssetId { get; set; }
    public TradeSide Side { get; set; }
    public int? StepNo { get; set; }
    public int? MaxSteps { get; set; }
    public decimal? StepAmount { get; set; }
    public DateOnly SignalDate { get; set; }
    public decimal ClosePrice { get; set; }
    public required JsonDocument Facts { get; set; }
    public SignalStatus Status { get; set; }
    public SkipReason? SkipReason { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public DateOnly? SnoozedUntil { get; set; }
    public DateOnly? EndedOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class SignalTrade
{
    public Guid Id { get; set; }
    public Guid SignalId { get; set; }
    public Guid StrategyId { get; set; }
    public Guid AssetId { get; set; }
    public DateOnly TradeDate { get; set; }
    public TradeSide Side { get; set; }
    public int? StepNo { get; set; }
    public decimal Price { get; set; }
    public decimal Shares { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal FxRate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class UserTrade : IHasUpdatedAt
{
    public Guid Id { get; set; }
    public Guid StrategyId { get; set; }
    public Guid AssetId { get; set; }
    public DateOnly TradeDate { get; set; }
    public TradeSide Side { get; set; }
    public decimal Price { get; set; }
    public EnteredAs EnteredAs { get; set; }
    public decimal Amount { get; set; }
    public decimal Shares { get; set; }
    public decimal Fee { get; set; }
    public decimal FxRate { get; set; }
    public FxSource FxSource { get; set; }
    public TradeSource Source { get; set; }
    public Guid? SignalId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A setting from the admin page (Admin/SettingDefinitions.cs). A secret's value is encrypted.
/// A null value means "cleared": it hides the server's value too.
/// </summary>
public class AppSetting : IHasUpdatedAt
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? SignalId { get; set; }
    public NotificationKind Kind { get; set; }
    public NotificationChannel Channel { get; set; }
    public DateOnly ForDate { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>One trading day of an asset, in its currency. Adjusted for splits and dividends, as the POC's Yahoo prices are.</summary>
public class PriceDaily
{
    public Guid AssetId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

/// <summary>Units of <see cref="Quote"/> per one unit of <see cref="Base"/> on a day (USD per EUR), from the day's close.</summary>
public class FxRate
{
    public DateOnly Date { get; set; }
    public required string Base { get; set; }
    public required string Quote { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>
/// Where a stock stands under a strategy's rules on a trading day (the engine's verdict at that day's close), which
/// the strategy page shows as its status. One row per strategy, stock and evaluated day.
/// </summary>
public class DailyEvaluation
{
    public Guid StrategyId { get; set; }
    public Guid AssetId { get; set; }
    public DateOnly Date { get; set; }
    public EvaluationStatus Status { get; set; }
    /// <summary>The step a buy signal is for, else null.</summary>
    public int? StepNo { get; set; }
    public required JsonDocument Facts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A comment or issue about the app, submitted by a user with a confirmed email (from selected text on a screen,
/// or by the owner from the feedback box) and reviewed by the owner.
/// </summary>
public class FeedbackItem : IHasUpdatedAt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public FeedbackKind Kind { get; set; }
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;
    public required string Text { get; set; }
    public string? Route { get; set; }
    public string? Element { get; set; }
    public string? SelectedText { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One AI round of a <i>Your AI's top picks</i> strategy (spec: AI round): due on a day, then done (the user pasted their AI's
/// answer) or skipped (a week went by). A done round keeps what the user copied and pasted, and the scores read from it.
/// </summary>
public class AiRound : IHasUpdatedAt
{
    public Guid Id { get; set; }
    public Guid StrategyId { get; set; }
    public DateOnly DueOn { get; set; }
    /// <summary>
    /// A round the user ran before the scheduled one was due: done on the day it was run (<c>DueOn</c>), leaving the schedule as
    /// it was. At most one a day.
    /// </summary>
    public bool Extra { get; set; }
    public AiRoundStatus Status { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
    /// <summary>The last finished US session when it was done: its signals' date, and where "picks vs the rest" starts.</summary>
    public DateOnly? SessionDate { get; set; }
    public int? PromptVersion { get; set; }
    public bool PromptEdited { get; set; }
    public string? PromptText { get; set; }
    public string? AiModel { get; set; }
    public string? Answer { get; set; }
    /// <summary>The scores read from the answer: <c>[{assetId, overall, news, mood, figures, reason, picked}]</c>.</summary>
    public JsonDocument? Scores { get; set; }
    /// <summary>Its shared copy, when the user shared it: the only link between the two, kept on the user's side, to remove it.</summary>
    public Guid? SharedRoundId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// An AI round a user chose to share (spec: AI data sharing). Kept apart from account data, with nothing that points back to
/// the user, the strategy or its budget: once stored it can't be traced or withdrawn, only future sharing stopped.
/// </summary>
public class SharedAiRound
{
    public Guid Id { get; set; }
    public DateOnly RoundOn { get; set; }
    public required string AiModel { get; set; }
    public int PromptVersion { get; set; }
    public bool PromptEdited { get; set; }
    /// <summary>The sources on: <c>["news","mood","figures"]</c>.</summary>
    public required JsonDocument Sources { get; set; }
    /// <summary>The stocks' symbols, in the prompt's order.</summary>
    public required JsonDocument Stocks { get; set; }
    /// <summary>The answer, with what looks like an email, a phone number or a link's parameters removed.</summary>
    public required string Answer { get; set; }
    /// <summary><c>[{symbol, overall, news, mood, figures}]</c>.</summary>
    public required JsonDocument Scores { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
