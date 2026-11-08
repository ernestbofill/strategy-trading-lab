// DTOs shared with the backend. Shapes mirror the .NET API (camelCase JSON): backend/src/StrategyTradingLab.Api/Templates/TemplateDtos.cs.

export type TemplateKey = 'dip' | 'comeback' | 'bear' | 'trend' | 'ai'

export type InvestsIn = 'stockList' | 'sp500Fund' | 'indexFund'

export type SignalSides = 'buysOnly' | 'buysAndSells'

/** A template as listed on the pick screen. */
export interface StrategyTemplate {
  key: TemplateKey
  /** Rule set the engine runs, as named in the POC (null for new templates). */
  engineKey: string | null
  name: string
  tagline: string
  idea: string
  investsIn: InvestsIn
  signals: SignalSides
  /** Set when signals come from the user's AI rounds instead of price rules (Your AI's top picks). */
  aiRound: AiRound | null
}

export interface AiRound {
  everyDays: number
  /** How many top-scored stocks get a step per round. */
  topN: number
}

/** A US-listed stock or fund. */
export interface Security {
  id: string
  symbol: string
  name: string
  /** One line on what the company does, or what the fund tracks. */
  what: string
}

/**
 * One line of "It signals when". `marker` is "01", "BUY", "SELL"…
 * In `text`, **bold** marks the rule's plain label and `backticks` mark numbers and terms.
 */
export interface Rule {
  marker: string
  tone?: 'buy' | 'sell'
  text: string
}

export type Watches = { kind: 'stocks'; starterList: Security[] } | { kind: 'fund'; fund: Security }

/** One result of the stock and fund search: backend Assets/AssetEndpoints.cs (AssetMatch). */
export interface AssetMatch {
  /** Null until it is added: only the price source knows it yet. */
  id: string | null
  symbol: string
  name: string
  /** The exchange's MIC ("XNYS"). */
  exchange: string
  what: string
}

/** What adding a stock or fund gave: backend Assets/AssetEndpoints.cs (AddedAssetDto). */
export interface AddedAsset {
  asset: Security
  /** False while its price history isn't in: the strategy waits for it. */
  pricesIn: boolean
}

export interface PeriodResult {
  period: string
  /** Yearly-average % return of the template and the index, each one's worst calendar year, direct alpha, and optional story. Null when the strategy never triggered in that period. */
  templatePerYear: number | null
  indexPerYear: number | null
  worstYear: number | null
  indexWorstYear: number | null
  alpha?: number | null
  story?: string | null
}

export interface BenchmarkBacktest {
  key: string
  name: string
  periods: PeriodResult[]
}

export interface Backtest {
  /** "1,146 stocks, 2000-2026." */
  scope: string
  /** Sub-periods, then the whole period last (shown on the review). */
  periods: PeriodResult[]
  /** Facts for "How we tested". */
  provider: string
  years: string
  universe: string
  testedDefaults: string
  /** The tested fee per trade: a % of the amount plus a fixed amount, 1 in the strategy's currency (€1, $1 or £1, rounded on purpose). */
  feePerTrade: number
  feePercent: number
  /** Optional multi-index backtest tabs (used by Index trend). */
  benchmarks?: BenchmarkBacktest[] | null
}

/** Euro amounts. `step` and `maxSteps` are null for all-in, all-out templates. */
export interface Budget {
  amount: number
  step: number | null
  maxSteps: number | null
}

/** What a new strategy from this template starts with. `params` is the template's own rule settings. */
export interface TemplateDefaults {
  version: number
  budget: Budget
  params: Record<string, number | boolean>
}

/** Everything the review screen (2 / 5) and How we tested (2c) show. */
export interface TemplateDetail extends StrategyTemplate {
  /** "The idea" on the review screen. */
  rationale: string
  rules: Rule[]
  watches: Watches
  /** Null when the template can't be backtested. */
  backtest: Backtest | null
  defaults: TemplateDefaults
  /** The rules Advanced settings can tune, in screen order; empty when the template has none yet. */
  settings: Setting[]
  /** The groups the settings are shown in, in screen order; empty when they are one plain list. */
  settingGroups: SettingGroup[]
}

/**
 * A rule on Advanced settings. `key` is its name in `defaults.params`, which holds the tested value. `sentence` has
 * `{value}` where the value goes; `value` reads a number, with `{n}` for it ("{n}%", "{n} days"). The screen offers
 * `min` to `max` in steps of `step`; `below` names the setting this one stays at least a step under.
 */
export interface Setting {
  key: string
  sentence: string
  value: string
  step: number
  min: number
  max: number
  below?: string
  /** What changing it does. */
  help?: string
  /** The key of its `SettingGroup`. */
  group?: string
}

/** A titled group on Advanced settings: the part of the rules its settings tune. `lead` may hold `term` markup. */
export interface SettingGroup {
  key: string
  title: string
  lead: string
}

export type StrategyMode = 'simulation' | 'live'

/** What a strategy has earned since it started, next to the index fund. */
export interface StrategyResults {
  /** Percent return, to one decimal. */
  strategy: number
  /** Null when the index fund's prices are missing. */
  index: number | null
  /** The strategy's value over time, oldest first, for the small line chart. */
  history: number[]
}

/** How the template did in the past, shown while a strategy has no results of its own. */
export interface PastResult {
  /** "2000-2024". */
  years: string
  /** The yearly rate that compounds to the template's and the S&P 500 fund's return over those years. */
  templatePerYear: number
  indexPerYear: number
  /** Percent return of each one's worst calendar year in that time. */
  worstYear: number
  indexWorstYear: number
  isCustomized?: boolean
}

/** A strategy as shown on the dashboard. */
export interface StrategySummary {
  id: string
  name: string
  templateKey: TemplateKey
  templateName: string
  mode: StrategyMode
  /** Local date, "2026-10-13". */
  startedOn: string
  /** The strategy's own currency, fixed when it started. */
  currency: Currency
  budget: number
  used: number
  /** Null until the strategy has trades (or, for AI templates, a first round). */
  results: StrategyResults | null
  /** Null for templates that can't be backtested. */
  pastResult: PastResult | null
  status: StrategyStatus
  /** The local date it was archived; its results stop there. Null while it runs. */
  endedOn: string | null
  /** The index fund's gain since the start day, in percent; null without its prices. */
  indexSinceStart: number | null
  /** Its stocks by pill; null once archived. */
  stocks: StockCounts | null
  /** The symbol of the fund a fund template follows; null for a stock list. */
  fund: string | null
}

/** A strategy's stocks by pill, for its card. `close`: candidates within the template's getting close % (3 unless the owner changed it) of the buy rule, not counted again in `candidates`. */
export interface StockCounts {
  signals: number
  close: number
  candidates: number
  holding: number
  notCandidates: number
}

/** A running strategy is checked every day; an archived one (`ended`) keeps its history, read-only. */
export type StrategyStatus = 'running' | 'ended'

/** Something that needs the user, newest first on the dashboard. */
export interface WaitingItem {
  id: string
  kind: 'buySignal' | 'sellSignal' | 'aiRound'
  strategyId: string
  strategyName: string
  /** The stock a buy signal is for. */
  stockName?: string
}

export interface Dashboard {
  /** The server's local date, "2026-10-17", to tell a strategy started today from an older one. */
  today: string
  /** Running ones first, then the archived ones, last archived first. */
  strategies: StrategySummary[]
  waiting: WaitingItem[]
}

/** The budget and what the strategy's buys use of it. Steps are null for all-in, all-out templates. */
export interface BudgetUsed {
  amount: number
  used: number
  step: number | null
  maxSteps: number | null
  /** Steps bought (live: confirmed) and how many the budget holds. */
  steps: number | null
  totalSteps: number | null
}

/** A point of the results chart: each line's gain in percent that day. */
export interface ResultPoint {
  date: string
  you: number
  rules: number | null
  index: number | null
}

/** A strategy's gain since it started, in percent. `rules` is live only: the rules' own trades. */
export interface StrategyFigures {
  you: number
  rules: number | null
  index: number | null
  history: ResultPoint[]
}

/** How a live strategy's signals were answered. */
export interface SignalCounts {
  did: number
  skipped: number
  missed: number
}

export type StockStatus =
  | 'waitingForYou'
  | 'signal'
  | 'holding'
  | 'done'
  | 'building'
  | 'outOfTheMarket'
  | 'noBudget'
  | 'watching'
  | 'notCandidate'
  | 'notEnoughData'
  /** Unticked with Edit list: no more signals; its row stays while it has trades. */
  | 'removed'
  | 'notChecked'

export type TradeSide = 'buy' | 'sell'

/** A stock of a strategy on its page. */
export interface StockRow {
  assetId: string
  symbol: string
  name: string
  status: StockStatus
  /** Steps the results count (live: the user's confirmed buys). */
  steps: number
  /** The rules' own steps, live only. */
  rulesSteps: number | null
  maxSteps: number | null
  /** How far the price is from the rules, in percent (0 when there or past it). */
  distancePct: number | null
  /** The waiting signal (live) or today's (simulation). */
  signalId: string | null
  signalStep: number | null
  signalSide?: TradeSide | null
  /** What was put in, fees included, and its gain in percent (null with no buys). */
  invested: number
  result: number | null
  /** AI strategies: its overall score in the last AI round that scored it. */
  aiScore?: number | null
  /** For the stock page: where it trades, its currency and what it does. */
  exchange: string
  currency: string
  what: string | null
  /** The day of its latest check, that day's close, when it became a candidate, and each rule's verdict. */
  checkedOn: string | null
  close: number | null
  candidateSince?: string | null
  checks: RuleCheck[]
}

/** The part of the rules a check belongs to: what makes a candidate, what gives a buy or sell signal, what earns the next step. */
export type CheckGroup = 'candidate' | 'buy' | 'sell' | 'next'

/**
 * One rule's verdict on the latest checked day: backend Engine/BuyRules.cs (RuleCheck). `met` is a boolean verdict;
 * `progress` (0 to 1+, exceeding 1 past the threshold) is how far a figure that must grow has come; `used` marks a
 * milestone that earned its step; `preview` marks next-step rules shown before the first buy.
 */
export interface RuleCheck {
  key: string
  group: CheckGroup
  met: boolean | null
  value: number | null
  min: number | null
  max: number | null
  level: number | null
  progress: number | null
  used: boolean | null
  date?: string | null
  midLevel?: number | null
  endLevel?: number | null
  subValue?: number | null
  endValue?: number | null
  preview?: boolean | null
}

/** A template's words for a check, with {value}, {+value}, {min}, {max}, {level}, {midLevel}, {endLevel}, and {date}; `past` when past `max`, `none` when no value. */
export interface CheckTexts {
  label: string
  detail: string
  short: string
  past?: string | null
  pastShort?: string | null
  none?: string | null
  info?: string | null
}

/** The template's words for a stock that isn't bought: "the discount zone", "Below its long trend". */
export interface StockStatusWords {
  zone: string
  notCandidate: string
  watching?: string | null
  holding?: string | null
}

/** A day of the stock page's chart: its close and the rules' checks that day (only candidate ones before `startedOn`). Backend Strategies/StrategyDtos.cs (ChartDay). */
export interface ChartDay {
  date: string
  close: number
  checks: RuleCheck[]
}

/** The stock page's chart: the last 90 days, and the day the rules first checked the stock under this strategy. */
export interface StockChart {
  startedOn: string | null
  days: ChartDay[]
}

/** The strategy page: backend Strategies/StrategyDtos.cs (StrategyDetail). */
export interface StrategyDetail {
  id: string
  name: string
  templateKey: TemplateKey
  templateName: string
  /** What the template invests in: a stock list or one fund. */
  investsIn: InvestsIn
  mode: StrategyMode
  status: StrategyStatus
  /** The local date it was archived; its results stop there. Null while it runs. */
  endedOn: string | null
  startedOn: string
  /** The user's local date. */
  today: string
  currency: Currency
  budget: BudgetUsed
  /** Null until the strategy has a buy. */
  results: StrategyFigures | null
  pastResult: PastResult | null
  /** Live only. */
  signals: SignalCounts | null
  /** Null for templates the engine doesn't run yet. */
  words: StockStatusWords | null
  stocks: StockRow[]
  trades: StrategyTradeItem[]
  signalHistory: StrategySignalItem[]
  rates: CurrencyRates[]
  settings?: Record<string, number> | null
  /** AI strategies only: the next round and how the picks did. */
  aiRounds?: AiRoundsSummary | null
  /** AI strategies only: what the prompt asks for. */
  ai?: AiSetup | null
  /** A live copy: the simulation it came from ("Go live with these settings"), while that exists. */
  sourceStrategyId?: string | null
  sourceStrategyName?: string | null
  /** The template's words for each rule check, by key. */
  checkTexts?: Record<string, CheckTexts> | null
  /** The template version it started from: its settings screen shows that version's values as tested. */
  defaultsVersion?: number
}

/** What the AI prompt asks for: the sources on, and the user's own instructions (null for the suggested ones). */
export interface AiSetup {
  news: boolean
  mood: boolean
  figures: boolean
  instructions: string | null
}

/** The prompt for the review and the editor, before a strategy exists. */
export interface AiPromptRequest extends AiSetup {
  everyDays: number
  /** The ticked stocks. */
  assetIds: string[]
}

export interface AiPromptPreview {
  version: number
  text: string
  /** The suggested instructions for these sources: what "Reset to suggested" puts back. */
  suggested: string
}

export interface AiStock {
  assetId: string
  symbol: string
  name: string
}

/** The AI round screen: backend Ai/AiRoundDtos.cs (AiRoundDetail). */
export interface AiRoundDetail {
  strategyId: string
  strategyName: string
  templateName: string
  mode: StrategyMode
  /** The strategy's own currency. */
  currency: Currency
  roundId: string
  dueOn: string
  /** The last day it can be run; after it the round is skipped. */
  lastDay: string
  /** When the round after it is due, once it is run or skipped. */
  nextDueOn: string
  /** Whether it can be run today. Before that, an extra round can be (the schedule stays). */
  open: boolean
  today: string
  promptVersion: number
  promptEdited: boolean
  prompt: string
  setup: AiSetup
  suggested: string
  stocks: AiStock[]
  everyDays: number
  topN: number
  lastModel: string | null
  sharing: boolean
  /** Whether the user has answered the sharing question (the consent box shows until then). */
  sharingAsked: boolean
}

/** A stock's scores as read from the answer, with the step it gets (or got). */
export interface AiScore {
  assetId: string
  symbol: string
  name: string
  overall: number
  news: number | null
  mood: number | null
  figures: number | null
  reason: string
  step: number | null
}

/** What was read from a pasted answer. `short`: why fewer stocks than the top N get a step. */
export interface AiReading {
  scores: AiScore[]
  problems: string[]
  /** A stock without a score or one that isn't yours: shown, but the scores can still be used. */
  warnings: string[]
  ok: boolean
  maxSteps: number | null
  stepAmount: number | null
  short: 'budget' | 'allSteps' | 'unavailable' | null
}

export interface AiRoundResult {
  roundId: string
  signalIds: string[]
  mode: StrategyMode
}

/** The picks' and the rest's average price change since their rounds, in percent. */
export interface PicksVsRest {
  picks: number
  rest: number
  rounds: number
}

export interface AiRoundItem {
  id: string
  dueOn: string
  /** Run before the scheduled round was due: dated the day it was run. */
  extra: boolean
  status: 'due' | 'done' | 'skipped'
  doneAt: string | null
  sessionDate: string | null
  aiModel: string | null
  promptVersion: number | null
  promptEdited: boolean
  prompt: string | null
  answer: string | null
  scores: AiScore[]
  picks: number | null
  rest: number | null
}

export interface AiRoundHistory {
  strategyId: string
  strategyName: string
  mode: StrategyMode
  /** The user's local date. */
  today: string
  rounds: AiRoundItem[]
  picksVsRest: PicksVsRest | null
}

/** On the strategy page: the next round (due now or later) and the picks against the rest. */
export interface AiRoundsSummary {
  nextDueOn: string | null
  due: boolean
  done: number
  skipped: number
  picksVsRest: PicksVsRest | null
}

export type EnteredAs = 'amount' | 'shares'
export type FxSource = 'reference' | 'user'
export type TradeSource = 'signal' | 'manual'

export interface RateDay {
  date: string
  rate: number
}

export interface CurrencyRates {
  currency: string
  rates: RateDay[]
}

export interface StrategyTradeItem {
  id: string
  assetId: string
  symbol: string
  stockName: string
  tradeDate: string
  side: TradeSide
  price: number
  assetCurrency: string
  enteredAs: EnteredAs
  amount: number
  shares: number
  fee: number
  strategyCurrency: Currency
  fxRate: number
  fxSource: FxSource
  source: TradeSource
  signalId: string | null
}

export interface StrategySignalItem {
  id: string
  assetId: string
  symbol: string
  stockName: string
  signalDate: string
  side: TradeSide
  stepNo: number | null
  maxSteps: number | null
  stepAmount: number | null
  closePrice: number
  assetCurrency: string
  reason: string
  status: SignalStatus
  skipReason: SkipReason | null
  snoozedUntil: string | null
  endedOn: string | null
  createdAt: string
}

export interface ManualTradeRequest {
  assetId: string
  side: TradeSide
  date: string | null
  price: number | null
  enteredAs: EnteredAs | null
  amount: number | null
  shares: number | null
  fee: number | null
  fxSource: FxSource | null
  fxRate: number | null
}

export interface UpdateTradeRequest {
  date: string | null
  price: number | null
  enteredAs: EnteredAs | null
  amount: number | null
  shares: number | null
  fee: number | null
  fxSource: FxSource | null
  fxRate: number | null
}

export type SignalStatus = 'waiting' | 'confirmed' | 'skipped' | 'missed'

export type SkipReason = 'priceRanAway' | 'didntTrustIt' | 'noCash' | 'other'

/** How the user answered a signal: the trade they saved, or the skip. */
export interface SignalAnswer {
  status: SignalStatus
  skipReason: SkipReason | null
  date: string | null
  price: number | null
  amount: number | null
  shares: number | null
  fee: number | null
}

/** Confirm a signal: backend Signals/SignalDtos.cs (SignalDetail). Prices are in the asset's currency, amounts in the strategy's. */
export interface SignalDetail {
  id: string
  strategyId: string
  strategyName: string
  templateName: string
  strategyCurrency: Currency
  stockName: string
  /** ISO code; may be one the user can't pick ("JPY"). */
  assetCurrency: string
  stepNo: number | null
  maxSteps: number | null
  stepAmount: number | null
  signalDate: string
  closePrice: number
  /** What the rules found: "8.2% below its recent peak, still above its 50-day average". */
  reason: string
  status: SignalStatus
  snoozedUntil: string | null
  endedOn: string | null
  /** The user's local date. */
  today: string
  /** The next trading day, its opening price once stored (else null), the step without the tested fee, and held shares for a sale. */
  prefill: { date: string; price: number | null; amount: number | null; shares?: number | null }
  /** Reference rates by day: asset currency per one unit of the strategy's. */
  rates: { date: string; rate: number }[]
  answer: SignalAnswer | null
  side?: TradeSide
  canSnooze?: boolean
}

/** "I did it". `fxRate` is only read when `fxSource` is 'user'. */
export interface SignalTrade {
  date: string | null
  price: number
  enteredAs: 'amount' | 'shares'
  amount: number | null
  shares: number | null
  fee: number | null
  fxSource: 'reference' | 'user'
  fxRate: number | null
}

/** What the Confirm screen sends to start a strategy: the choices of the five steps. Its other settings start at the template's tested defaults. */
export interface NewStrategy {
  templateKey: TemplateKey
  name: string
  mode: StrategyMode
  budget: Budget
  /** The currency the user saw while setting the budget. */
  currency: Currency
  /** The stocks to watch, or the one fund. */
  assetIds: string[]
  /** Every Advanced setting (setting key → value): the user's changes over the defaults shown. */
  settings: Record<string, number>
  /** Templates driven by AI rounds: what the prompt asks for. */
  ai?: AiSetup
  /** A live strategy copied from one of the user's simulations ("Go live with these settings"). */
  sourceStrategyId?: string
  /** The template version the screens showed: the strategy is stamped with it. */
  defaultsVersion?: number
}

/** What the browser's `PushSubscription.toJSON()` gives: backend Notifications/WebPushEndpoints.cs. */
export interface PushSubscriptionJson {
  endpoint: string
  keys: { p256dh: string; auth: string }
}

/** A setting on the admin page. A secret's `value` is never sent: only whether it is set. */
export interface AdminSetting {
  key: string
  group: string
  description: string
  default: string | null
  secret: boolean
  value: string | null
  isSet: boolean
  /** Where the value comes from: this page, the server's environment, the default, or nowhere. */
  source: 'page' | 'server' | 'default' | 'none'
}

export type Locale = 'en' | 'ca'

/** The ISO code of a currency a user can pick (see `CURRENCIES` in `lib/format.ts`). */
export type Currency = 'EUR' | 'USD' | 'GBP'

/** What an account may do: the owner runs the app (the admin page), a vip is invited, a user is everyone else. */
export type Role = 'user' | 'vip' | 'owner'

export type FeedbackKind = 'bug' | 'idea' | 'note' | 'feature'

export type FeedbackStatus = 'open' | 'closed'

/** A feedback comment, optionally pinned to selected text on a screen: backend Feedback/FeedbackEndpoints.cs (FeedbackDto). */
export interface FeedbackItem {
  id: string
  userEmail: string
  kind: FeedbackKind
  status: FeedbackStatus
  text: string
  route: string | null
  element: string | null
  selectedText: string | null
  createdAt: string
  updatedAt: string
  closedAt: string | null
}

export interface NewFeedback {
  kind: FeedbackKind
  text: string
  route?: string | null
  element?: string | null
  selectedText?: string | null
}

export interface FeedbackPatch {
  kind?: FeedbackKind
  status?: FeedbackStatus
  text?: string
}

export interface SummaryWaitingSignal {
  signalId: string
  strategyId: string
  strategyName: string
  stockName: string
  side: 'buy' | 'sell'
  signalDate: string
  stepNo: number | null
  maxSteps: number | null
  closePrice: number
}

export interface SummaryWaitingAiRound {
  strategyId: string
  strategyName: string
}

export interface SummaryChangedSignal {
  signalId: string
  strategyId: string
  strategyName: string
  mode: StrategyMode
  stockName: string
  side: 'buy' | 'sell'
  signalDate: string
  closePrice: number
  stepNo: number | null
  maxSteps: number | null
  text: string
}

export interface SummaryStockChange {
  strategyId: string
  assetId: string
  strategyName: string
  mode: StrategyMode
  stockName: string
  kind: 'becameCandidate' | 'noLongerCandidate' | 'cameClose'
  distancePct: number | null
  text: string
}

export interface SummaryStrategyScore {
  strategyId: string
  strategyName: string
  mode: StrategyMode
  startedOn: string
  signalsCount: number
}

/** The daily summary: backend Notifications/DailySummaryDtos.cs (DailySummaryDto). */
export interface DailySummary {
  date: string
  hasChanges: boolean
  waitingSignals: SummaryWaitingSignal[]
  waitingAiRounds: SummaryWaitingAiRound[]
  whatChanged: SummaryChangedSignal[]
  stockChanges: SummaryStockChange[]
  strategies: SummaryStrategyScore[]
}

/** The logged-in user: backend Auth/AccountEndpoints.cs (AccountDto). */
export interface Account {
  email: string
  /** Asked at sign-up since invites; older accounts have none until they add it. */
  firstName: string | null
  lastName: string | null
  emailVerified: boolean
  locale: Locale
  timeZone: string
  /** The summary by email, after the US market close on days something changed. */
  overviewEmail: boolean
  /** The summary by web push, on the devices the user turned on. */
  overviewPush: boolean
  /** Signal alerts and reminders by email. */
  alertEmails: boolean
  /** Signal alerts and reminders by web push, on the devices the user turned on. */
  alertPush: boolean
  /** Of the user's new strategies; running ones keep theirs. */
  currency: Currency
  role: Role
  /** Shares AI rounds anonymously. */
  aiSharing: boolean
  termsVersionAccepted?: string | null
  termsAcceptedAt?: string | null
}

/** What Sign-up sends. */
export interface SignUp {
  firstName: string
  lastName: string
  email: string
  password: string
  locale: Locale
  /** IANA, e.g. "Europe/Madrid". */
  timeZone: string
  currency: Currency
  disclaimerAccepted: boolean
  /** The accepted legal terms version. */
  termsVersion?: string
  /** From an invite link or typed; needed while sign-ups are invite only. */
  inviteCode?: string
}

/** Who an invite is for: Sign-up greets them and fills in what is known. */
export interface SignUpInvite {
  code: string
  firstName: string
  locale: Locale
  /** The inviter's first name, when they have one. */
  invitedBy: string | null
}

/** What Sign-up asks first: backend Auth/AccountEndpoints.cs (SignUpOptions). */
export interface SignUpOptions {
  inviteOnly: boolean
  /** Null without a code, or when the code can't be used (revoked or used up). */
  invite: SignUpInvite | null
}

/** Someone who signed up with an invite. */
export interface InvitedUser {
  email: string
  firstName: string | null
  lastName: string | null
  joinedAt: string
}

/** An invite on /admin/invites: backend Invites/InviteEndpoints.cs (AdminInvite). */
export interface AdminInvite {
  id: string
  code: string
  link: string
  /** Ready to copy into WhatsApp or anywhere else, in the invite's language. */
  message: string
  firstName: string
  lastName: string | null
  email: string | null
  locale: Locale
  role: Role
  maxUses: number
  uses: number
  createdAt: string
  sentAt: string | null
  revokedAt: string | null
  joined: InvitedUser[]
}

export interface NewInvite {
  firstName: string
  lastName?: string
  email?: string
  locale: Locale
  /** Also email it to `email`. */
  send?: boolean
}

/** The API's build, and the database it uses (the top bar shows DEV for the dev one). */
export interface BackendVersion {
  version: string
  commit: string | null
  /** ISO time; null in Debug builds. */
  builtAt: string | null
  database: DatabaseName
}

export type DatabaseName = 'dev' | 'production'

/** A database on the admin page. Its URL is set only on the server and holds a password, so only the host is sent. */
export interface AdminDatabase {
  name: DatabaseName
  /** The server variable its URL comes from: DATABASE_URL for the server's own, else DATABASE_URL_DEV or _PRODUCTION. */
  variable: string
  /** Null when the variable isn't set. */
  host: string | null
}

export interface AdminDatabases {
  active: DatabaseName
  /** The server's own database, the one its environment points at. */
  home: DatabaseName
  databases: AdminDatabase[]
}

/** A symbol the last price refresh couldn't update. */
export interface PriceProblem {
  name: string
  problem: string | null
}

/** The admin page's prices box: backend Admin/AdminEndpoints.cs (AdminPrices). Times are ISO strings, days "2026-10-22". */
export interface AdminPrices {
  running: boolean
  /** Null while the daily schedule is off. */
  nextRun: string | null
  lastStarted: string | null
  lastFinished: string | null
  latestDay: string | null
  problems: PriceProblem[]
  updated: number
  fullHistory: number
  /** Signals the rules created after the last refresh; null before the first run or when the evaluation failed. */
  newSignals: number | null
  /** Strategies the last evaluation couldn't run. */
  evaluationFailures: number
}

/** What Yahoo answered for one symbol in "Test Yahoo". */
export interface YahooCheck {
  symbol: string
  ok: boolean
  day: string | null
  close: number | null
  problem: string | null
  milliseconds: number
}

export type AssetKind = 'stock' | 'fund' | 'index'

/** An account on the admin users page: backend Admin/AdminUsers.cs (AdminUser). */
export interface AdminUser {
  id: string
  email: string
  role: Role
  joined: string
  confirmed: boolean
  /** Why the mail service says the address can't be mailed (the app no longer sends it the overview or alerts); null when it can. */
  emailBlocked: 'bounced' | 'invalid' | 'complaint' | null
  /** The last time any of its sessions was used; null when none is kept. */
  lastVisit: string | null
  running: number
  archived: number
  firstName: string | null
  lastName: string | null
  /** The email of whoever made the invite it signed up with; null without one. */
  invitedBy: string | null
}

/** Running strategies of one template, by mode. */
export interface AdminTemplateCount {
  templateKey: TemplateKey
  templateName: string
  simulation: number
  live: number
}

/** The admin users page: backend Admin/AdminUsers.cs (AdminUsers). Users newest first. */
export interface AdminUsers {
  counts: { users: number; confirmed: number; running: AdminTemplateCount[] }
  users: AdminUser[]
}

/** What raised an admin alert. */
export type AlertSource = 'dailyCheck' | 'prices' | 'strategies' | 'aiStrategies' | 'email'

/** Open; dismissed (quiet while the problem lasts); or cleared, when a later run found the problem gone. */
export type AlertStatus = 'open' | 'dismissed' | 'cleared'

/** A problem the background jobs found: backend Admin/AdminAlerts.cs (AdminAlertDto). `count`: times seen since it opened. */
export interface AdminAlert {
  id: string
  source: AlertSource
  status: AlertStatus
  title: string
  detail: string | null
  count: number
  firstSeenAt: string
  lastSeenAt: string
  closedAt: string | null
}


/** A template on /admin/templates: backend Admin/AdminTemplates.cs (AdminTemplate). Its settings and every version, newest first. */
export interface AdminTemplate {
  key: string
  name: string
  investsIn: InvestsIn
  /** False for a template whose stocks have no buy rule (the AI's): no "getting close". */
  hasClosePct: boolean
  settings: AdminTemplateSetting[]
  versions: AdminTemplateVersion[]
}

/** A setting of Advanced settings, with its words (`sentence` with `{value}`, `value` with `{n}`) and range. */
export interface AdminTemplateSetting {
  key: string
  sentence: string
  value: string
  step: number
  min: number
  max: number
  below?: string | null
}

/** A version of what new strategies start with. Version 1 is the tested one; one version is active. */
export interface AdminTemplateVersion {
  version: number
  active: boolean
  createdAt: string
  defaults: Record<string, number>
  /** The starter list, or the one fund, sorted by name. */
  assets: AdminTemplateAsset[]
  closePct: number
  /** How many strategies started from it. */
  strategies: number
}

export interface AdminTemplateAsset {
  id: string
  symbol: string
  name: string
  active: boolean
}

/** A new version: every setting's default, the stocks or the fund, "getting close". */
export interface AdminTemplateChange {
  defaults: Record<string, number>
  assetIds: string[]
  closePct: number | null
}

/** An asset on the admin assets page: backend Admin/AdminEndpoints.cs (AdminAsset). */
export interface AdminAsset {
  id: string
  symbol: string
  exchange: string
  name: string
  what: string
  kind: AssetKind
  currency: string
  active: boolean
  /** Running strategies that list this asset, ticked or not: its prices are fetched daily. */
  strategies: number
  /** Newest stored price day ("2026-10-22"), or null if none is stored yet. */
  latestPriceDate: string | null
  /** Why the last price fetch couldn't be used (Yahoo's error, bars that aren't daily), or null when it worked. */
  pricesProblem: string | null
}

/** What deleting every price did: backend Admin/AdminEndpoints.cs (PricesFlushed). */
export interface PricesFlushed {
  prices: number
  rates: number
}

/** What downloading the exchange rates did: backend Admin/AdminEndpoints.cs (RatesDownloaded). */
export interface RatesDownloaded {
  rows: number
  /** The currencies that failed, with why. */
  problems: PriceProblem[]
}

/** What adding a symbol did: backend Admin/AdminEndpoints.cs (AdminAddedAsset). */
export interface AdminAddedAsset {
  asset: AdminAsset
  /** False when the asset was already there. */
  created: boolean
  /** Why its prices or exchange rate didn't load; adding it again tries again. */
  problem: string | null
}

export interface Api {
  /** The logged-in user, or null without a session: the app's start-up check. */
  getAccount(): Promise<Account | null>
  /** Creates an account and logs it in; the same answer when the email already has one. Rejects with `InvalidFieldsError`, `TooManyTriesError`. */
  signUp(signUp: SignUp): Promise<void>
  /** Whether an invite is needed, and who `invite` (a code) is for. Rejects with `TooManyTriesError` when rate-limited. */
  getSignUpOptions(invite?: string): Promise<SignUpOptions>
  /** Rejects with `InvalidFieldsError` (`firstName`, `lastName`). */
  setName(firstName: string, lastName: string): Promise<void>
  /** Rejects with `UnauthorizedError` for a wrong email or password, `TooManyTriesError` while the email is locked. */
  logIn(email: string, password: string): Promise<void>
  logOut(): Promise<void>
  /** Confirms the email from its link; logs no one in. Rejects with `ExpiredLinkError`. */
  verifyEmail(token: string): Promise<void>
  resendVerification(): Promise<void>
  /** Always resolves the same, whether or not the email has an account. */
  forgotPassword(email: string): Promise<void>
  /** Sets the password from the reset link and logs this device in. Rejects with `ExpiredLinkError`, `InvalidFieldsError`. */
  resetPassword(token: string, password: string): Promise<void>
  /** Rejects with `InvalidFieldsError` (`currentPassword`, `newPassword`). */
  changePassword(currentPassword: string, newPassword: string): Promise<void>
  /** Sets the currency of the user's new strategies. Rejects with `InvalidFieldsError`. */
  setCurrency(currency: Currency): Promise<void>
  /** Sets the language of the user's account and emails. Rejects with `InvalidFieldsError`. */
  setLocale(locale: Locale): Promise<void>
  /** Sets the user's time zone (an IANA name). Rejects with `InvalidFieldsError`. */
  setTimeZone(timeZone: string): Promise<void>
  /** Turns the summary email on or off. */
  setOverviewEmail(enabled: boolean): Promise<void>
  /** Turns the summary web push notification on or off. */
  setOverviewPush(enabled: boolean): Promise<void>
  setAlertEmails(enabled: boolean): Promise<void>
  setAlertPush(enabled: boolean): Promise<void>
  /** The link in an email: turns that kind off for the user whose private token it holds. Rejects with `ExpiredLinkError` for an unknown token. */
  unsubscribe(token: string, kind: 'overview' | 'alerts'): Promise<void>
  getTemplates(): Promise<StrategyTemplate[]>
  /** The template as its active version, or as `version` (a strategy's own, for its settings screen). */
  getTemplate(key: TemplateKey, version?: number): Promise<TemplateDetail | undefined>
  /** Rejects with `UnauthorizedError` when the session has ended (also for `startStrategy`). */
  getDashboard(): Promise<Dashboard>
  /** The daily summary for the dedicated daily summary page. */
  getSummary(): Promise<DailySummary>
  /** The strategy page; undefined when the user has no such strategy. */
  getStrategy(id: string): Promise<StrategyDetail | undefined>
  /** A stock's last 90 days with each day's rule checks, oldest first; undefined when it isn't in the user's strategy. */
  getStockChart(strategyId: string, assetId: string): Promise<StockChart | undefined>
  /** Updates the settings (rules) of a running strategy. */
  updateStrategySettings(id: string, settings: Record<string, number>): Promise<void>
  /** Edit list: the stocks a running strategy follows from now on. Rejects with `InvalidFieldsError` (`assetIds`). */
  updateStrategyAssets(id: string, assetIds: string[]): Promise<void>
  /** A live signal for Confirm a signal; undefined when the user has no such signal. */
  getSignal(id: string): Promise<SignalDetail | undefined>
  /** Rejects with `InvalidFieldsError` (by field) or `InvalidValueError` (already answered). */
  confirmSignal(id: string, trade: SignalTrade): Promise<void>
  /** Log a trade by hand on one of the strategy's stocks (marked Manual). Rejects with `InvalidFieldsError`, `InvalidValueError`. */
  logManualTrade(strategyId: string, trade: ManualTradeRequest): Promise<StrategyTradeItem>
  /** Edit a trade's date, price, amount/shares, fee, or rate. Rejects with `InvalidFieldsError`, `InvalidValueError`. */
  updateTrade(tradeId: string, trade: UpdateTradeRequest): Promise<void>
  /** Soft-deletes a live trade. */
  deleteTrade(tradeId: string): Promise<void>
  skipSignal(id: string, reason: SkipReason | null): Promise<void>
  /** Resolves with the day the user is reminded. */
  snoozeSignal(id: string): Promise<string>
  /** Stored and new stocks or funds by name or symbol. Rejects with `InvalidValueError` (its message is for the user), `TooManyTriesError`. */
  searchAssets(query: string, kind: 'stock' | 'fund'): Promise<AssetMatch[]>
  /** Adds a search result that has no id yet and loads its prices. Rejects as `searchAssets` does, with `ForbiddenError` before the email is confirmed, and with `InvalidValueError` (the server's words) at the user's limit of new ones. */
  addAsset(symbol: string, kind: 'stock' | 'fund'): Promise<AddedAsset>
  /** Starts a strategy. Rejects with `NameTakenError` when the user already runs one with that name. */
  startStrategy(strategy: NewStrategy): Promise<StrategySummary>
  /** Deletes an archived strategy for good, with its signals, trades, results and AI rounds. One already gone counts as deleted. */
  deleteStrategy(id: string): Promise<void>
  /** Rejects with `NameTakenError` when another running strategy has the name, `InvalidFieldsError` for a wrong length. */
  renameStrategy(id: string, name: string): Promise<void>
  /** The VAPID key to subscribe with; null when the server doesn't send push. */
  getPushKey(): Promise<string | null>
  /** Saves this device's push subscription for the logged-in user. */
  savePushSubscription(subscription: PushSubscriptionJson): Promise<void>
  /** Forgets this device's push subscription. */
  removePushSubscription(endpoint: string): Promise<void>
  /** Archives a running strategy: the daily check stops, its waiting signals become missed and its name is free. Rejects with `InvalidValueError` when it is archived already. */
  archiveStrategy(id: string): Promise<void>
  /** Admin page, for the owner. Each rejects with `UnauthorizedError` when not logged in, `ForbiddenError` for any other role, `TooManyTriesError` when rate-limited. */
  getAdminSettings(): Promise<AdminSetting[]>
  getAdminInvites(): Promise<AdminInvite[]>
  /** Rejects with `InvalidFieldsError` (`firstName`, `lastName`, `email`, `locale`). */
  createAdminInvite(invite: NewInvite): Promise<AdminInvite>
  /** Emails an unused invite again. Rejects with `InvalidValueError` saying why not. */
  sendAdminInvite(id: string): Promise<AdminInvite>
  revokeAdminInvite(id: string): Promise<void>
  /**
   * Rejects with `InvalidValueError` (its message is for the user) when the value is refused. A secret needs `shown`:
   * the values the page shows, so the API can refuse it if its mail server changed since (409).
   */
  setAdminSetting(key: string, value: string, shown?: Record<string, string | null>): Promise<void>
  /** Back to the server's value or the default; `clear` leaves no value at all (secrets). */
  resetAdminSetting(key: string, clear?: boolean): Promise<void>
  /** Sends a test email to the mail account itself and resolves with its address; rejects with `InvalidValueError` saying why not. */
  sendAdminTestEmail(): Promise<string>
  getAdminPrices(): Promise<AdminPrices>
  /** Starts a refresh in the background, whatever the schedule says. Rejects with `InvalidValueError` when one is running. */
  refreshAdminPrices(): Promise<void>
  /** Asks Yahoo for a fund and an exchange rate, and stores nothing. Rejects with `TooManyTriesError` after 30 an hour. */
  testAdminYahoo(): Promise<YahooCheck[]>
  /** Public: the API's build and the database it uses. */
  getVersion(): Promise<BackendVersion>
  getAdminDatabases(): Promise<AdminDatabases>
  /** Rejects with `InvalidValueError` saying why when refused (no URL on the server, a database that doesn't answer). */
  switchAdminDatabase(name: DatabaseName): Promise<void>
/** All assets, for the admin assets page. */
  getAdminAssets(): Promise<AdminAsset[]>
  getAdminUsers(): Promise<AdminUsers>
  /** The open and dismissed alerts, and those cleared in the last 7 days, last seen first. */
  getAdminAlerts(): Promise<AdminAlert[]>
  /** Dismisses an open alert: it stays quiet while its problem lasts. */
  dismissAdminAlert(id: string): Promise<void>
  /** Every template: what new strategies start with, next to the tested values. */
  getAdminTemplates(): Promise<AdminTemplate[]>
  /** Saves a new version and makes it active. Rejects with `InvalidFieldsError` (`settings.<key>`, `assetIds`, `closePct`). */
  saveAdminTemplateVersion(key: string, change: AdminTemplateChange): Promise<AdminTemplate>
  /** Makes a version active: new strategies of the template start with it. */
  activateAdminTemplateVersion(key: string, version: number): Promise<AdminTemplate>
  /** Hides or restores an asset. */
  setAdminAssetActive(id: string, active: boolean): Promise<void>
  /** Adds an asset by its Yahoo symbol and loads its prices. Rejects with `InvalidFieldsError` (`symbol`), or `InvalidValueError` when Yahoo fails. */
  addAdminAsset(symbol: string): Promise<AdminAddedAsset>
  /** Changes an asset's name and English description. Rejects with `InvalidFieldsError` (`name`, `what`). */
  updateAdminAsset(id: string, edit: { name: string; what: string }): Promise<void>
  /** Deletes every stored price and exchange rate. */
  flushAdminPrices(): Promise<PricesFlushed>
  /** Deletes one asset's stored prices. */
  flushAdminAssetPrices(id: string): Promise<void>
  /**
   * Replaces one asset's prices with the daily prices of the last `years` years and returns its row. Rejects with
   * `InvalidFieldsError` (`years`), or `InvalidValueError` when Yahoo fails or the daily refresh is running.
   */
  downloadAdminAssetPrices(id: string, years: number): Promise<AdminAsset>
  /** Replaces the exchange rates of every currency in use with the last `years` years. */
  downloadAdminRates(years: number): Promise<RatesDownloaded>
/** Submits a feedback comment (requires a confirmed email). Rejects with `ForbiddenError`, `InvalidValueError` or `TooManyTriesError`. */
  submitFeedback(feedback: NewFeedback): Promise<FeedbackItem>
  /** Owner only: lists, edits, closes/reopens or deletes feedback comments. */
  getFeedback(status?: FeedbackStatus): Promise<FeedbackItem[]>
  updateFeedback(id: string, patch: FeedbackPatch): Promise<FeedbackItem>
  deleteFeedback(id: string): Promise<void>
  /** Public: the AI template's prompt for these sources, instructions and stocks. Rejects with `InvalidFieldsError`. */
  previewAiPrompt(request: AiPromptRequest): Promise<AiPromptPreview>
  /** The strategy's AI round that is due (or the next one); undefined for anything else. */
  getAiRound(strategyId: string): Promise<AiRoundDetail | undefined>
  /** Reads a pasted answer; saves nothing. */
  readAiAnswer(strategyId: string, answer: string): Promise<AiReading>
  /**
   * "Use these scores", with the prompt as the screen showed it. Rejects with `InvalidFieldsError` (`answer`, `aiModel`) or
   * `InvalidValueError` (not due, done already, or the prompt changed since).
   */
  useAiScores(strategyId: string, answer: string, aiModel: string, share: boolean | null, prompt: string, extra: boolean): Promise<AiRoundResult>
  /** Skips the round that is due. Rejects with `InvalidValueError` when it isn't due or changed meanwhile. */
  skipAiRound(strategyId: string, roundId: string): Promise<void>
  getAiRounds(strategyId: string): Promise<AiRoundHistory | undefined>
  /** A running AI strategy's prompt. Rejects with `InvalidFieldsError` (`sources`, `instructions`). */
  updateAiPrompt(strategyId: string, setup: AiSetup): Promise<void>
  /** Turns sharing AI rounds anonymously on or off. */
  setAiSharing(enabled: boolean): Promise<void>

}
