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
  everyWeeks: number
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
  /** The tested fee per trade: a % of the amount plus a fixed amount in euro. */
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
  | 'building'
  | 'noBudget'
  | 'watching'
  | 'notCandidate'
  | 'notEnoughData'
  | 'notChecked'

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
  /** What was put in, fees included, and its gain in percent (null with no buys). */
  invested: number
  result: number | null
}

/** The template's words for a stock that isn't bought: "the discount zone", "Below its long trend". */
export interface StockStatusWords {
  zone: string
  notCandidate: string
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
  /** Turns the daily overview email on or off. */
  setOverviewEmail(enabled: boolean): Promise<void>
  getTemplates(): Promise<StrategyTemplate[]>
  getTemplate(key: TemplateKey): Promise<TemplateDetail | undefined>
  /** Rejects with `UnauthorizedError` when the session has ended (also for `startStrategy`). */
  getDashboard(): Promise<Dashboard>
  /** The strategy page; undefined when the user has no such strategy. */
  getStrategy(id: string): Promise<StrategyDetail | undefined>
  /** Starts a strategy. Rejects with `NameTakenError` when the user already runs one with that name. */
  startStrategy(strategy: NewStrategy): Promise<StrategySummary>
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
}
