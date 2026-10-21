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
    public string? PasswordHash { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public required string TimeZone { get; set; }
    public required string Locale { get; set; }
    public string Currency { get; set; } = "EUR";
    public bool OverviewEmail { get; set; } = true;
    public DateTimeOffset? DisclaimerAcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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
