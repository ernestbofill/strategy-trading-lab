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

export interface PeriodResult {
  period: string
  /** Percent return of the template, the index fund, and the worst calendar year. */
  template: number
  index: number
  worstYear: number
}

export interface Backtest {
  /** "12 stocks, 2010-2024." */
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
  /** "2010-2024". */
  years: string
  template: number
  index: number
  /** Percent return of the template's worst calendar year in that time. */
  worstYear: number
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
}

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
}

/** The template's words for a stock that isn't bought: "the discount zone", "Below its long trend". */
export interface StockStatusWords {
  zone: string
  notCandidate: string
  watching?: string | null
  holding?: string | null
}

/** The strategy page: backend Strategies/StrategyDtos.cs (StrategyDetail). */
export interface StrategyDetail {
  id: string
  name: string
  templateKey: TemplateKey
  templateName: string
  mode: StrategyMode
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
  /** The Advanced settings the user changed (setting key → value); the rest keep the tested defaults. */
  settings: Record<string, number>
  /** Templates driven by AI rounds: what the prompt asks for. */
  ai?: AiSetup
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

/** The logged-in user: backend Auth/AccountEndpoints.cs (AccountDto). */
export interface Account {
  email: string
  emailVerified: boolean
  locale: Locale
  timeZone: string
  overviewEmail: boolean
  /** Of the user's new strategies; running ones keep theirs. */
  currency: Currency
  role: Role
  /** Shares AI rounds anonymously. */
  aiSharing: boolean
}

/** What Sign-up sends. */
export interface SignUp {
  email: string
  password: string
  locale: Locale
  /** IANA, e.g. "Europe/Madrid". */
  timeZone: string
  overviewEmail: boolean
  currency: Currency
  disclaimerAccepted: boolean
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
  /** Turns the daily overview email on or off. */
  setOverviewEmail(enabled: boolean): Promise<void>
  getTemplates(): Promise<StrategyTemplate[]>
  getTemplate(key: TemplateKey): Promise<TemplateDetail | undefined>
  /** Rejects with `UnauthorizedError` when the session has ended (also for `startStrategy`). */
  getDashboard(): Promise<Dashboard>
  /** The strategy page; undefined when the user has no such strategy. */
  getStrategy(id: string): Promise<StrategyDetail | undefined>
  /** Updates the settings (rules) of a running strategy. */
  updateStrategySettings(id: string, settings: Record<string, number>): Promise<void>
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
  /** Starts a strategy. Rejects with `NameTakenError` when the user already runs one with that name. */
  startStrategy(strategy: NewStrategy): Promise<StrategySummary>
  /** Deletes a strategy for good, with its signals, trades, results and AI rounds. One already gone counts as deleted. */
  deleteStrategy(id: string): Promise<void>
  /** Admin page, for the owner. Each rejects with `UnauthorizedError` when not logged in, `ForbiddenError` for any other role, `TooManyTriesError` when rate-limited. */
  getAdminSettings(): Promise<AdminSetting[]>
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
  /** Hides or restores an asset. */
  setAdminAssetActive(id: string, active: boolean): Promise<void>
  /** Adds an asset by its Yahoo symbol and loads its prices. Rejects with `InvalidFieldsError` (`symbol`), or `InvalidValueError` when Yahoo fails. */
  addAdminAsset(symbol: string): Promise<AdminAddedAsset>
  /** Changes an asset's name and English description. Rejects with `InvalidFieldsError` (`name`, `what`). */
  updateAdminAsset(id: string, edit: { name: string; what: string }): Promise<void>
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
