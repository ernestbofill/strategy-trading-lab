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

/// <summary>What a notification is about. <c>StockCandidate</c> and <c>StockClose</c> are push only: a stock in a strategy became a candidate or came close.</summary>
public enum NotificationKind { Signal, Snoozed, Overview, AiRound, SignalEnded, StockCandidate, StockClose }

public enum NotificationChannel { Push, Email }

public enum EmailTokenPurpose { VerifyEmail, ResetPassword }

/// <summary>What an account may do: Owner is the app's owner (the admin page), Vip is an invited user with extra privileges, User everyone else.</summary>
public enum UserRole { User, Vip, Owner }

/// <summary>An AI round: due until it is run (done) or its week runs out (skipped).</summary>
public enum AiRoundStatus { Due, Done, Skipped }

public enum FeedbackKind { Bug, Idea, Note, Feature }

public enum FeedbackStatus { Open, Closed }

/// <summary>Why an address is on the list the app never sends to (<see cref="EmailSuppression"/>): the mail service reported it.</summary>
public enum SuppressionReason { Bounced, Invalid, Complaint }

/// <summary>What raised an admin alert. Each source clears its own alerts once a run finds the problem gone.</summary>
public enum AlertSource { DailyCheck, Prices, Strategies, AiStrategies, Email }

/// <summary>
/// An admin alert: open; dismissed by the owner (it stays quiet while the problem lasts); or cleared, when a later run found
/// the problem gone. Only a cleared alert opens again, the next time its problem comes back.
/// </summary>
public enum AlertStatus { Open, Dismissed, Cleared }
