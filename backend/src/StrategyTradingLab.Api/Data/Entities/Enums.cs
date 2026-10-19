namespace StrategyTradingLab.Api.Data.Entities;

// Stored as snake_case text with a CHECK constraint (see SnakeCaseEnumConverter).
// A new member needs a migration that updates the matching check.

public enum AssetKind { Stock, Fund, Index }

public enum StrategyMode { Simulation, Live }

public enum StrategyStatus { Running, Ended }

public enum TradeSide { Buy, Sell }

public enum SignalStatus { Waiting, Confirmed, Skipped, Missed }

public enum SkipReason { PriceRanAway, DidntTrustIt, NoCash, Other }

public enum EnteredAs { Amount, Shares }

public enum FxSource { Reference, User }

public enum TradeSource { Signal, Manual }

public enum NotificationKind { Signal, Snoozed }

public enum NotificationChannel { Push, Email }
