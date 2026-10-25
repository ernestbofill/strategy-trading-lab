namespace StrategyTradingLab.Api.Data.Entities;

// Stored as snake_case text with a CHECK constraint (see SnakeCaseEnumConverter).
// A new member needs a migration that updates the matching check.

public enum AssetKind { Stock, Fund, Index }

public enum StrategyMode { Simulation, Live }

public enum StrategyStatus { Running, Ended }

public enum TradeSide { Buy, Sell }

/// <summary>Where a stock stands under a strategy's rules on a day: the engine's RuleStatus, stored.</summary>
public enum EvaluationStatus { NotEnoughData, NotCandidate, Watching, BuySignal, SignalEnded, Building, Holding, SellSignal }

public enum SignalStatus { Waiting, Confirmed, Skipped, Missed }

public enum SkipReason { PriceRanAway, DidntTrustIt, NoCash, Other }

public enum EnteredAs { Amount, Shares }

public enum FxSource { Reference, User }

public enum TradeSource { Signal, Manual }

public enum NotificationKind { Signal, Snoozed, Overview, AiRound }

public enum NotificationChannel { Push, Email }

public enum EmailTokenPurpose { VerifyEmail, ResetPassword }

/// <summary>What an account may do: Owner is the app's owner (the admin page), Vip is an invited user with extra privileges, User everyone else.</summary>
public enum UserRole { User, Vip, Owner }

/// <summary>An AI round: due until it is run (done) or its week runs out (skipped).</summary>
public enum AiRoundStatus { Due, Done, Skipped }

public enum FeedbackKind { Bug, Idea, Note, Feature }

public enum FeedbackStatus { Open, Closed }
