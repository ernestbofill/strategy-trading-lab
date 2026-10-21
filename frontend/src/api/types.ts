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
  index: number
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
  /** Euro. */
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

/** What the Confirm screen sends to start a strategy: the choices of the five steps. Its other settings start at the template's tested defaults. */
export interface NewStrategy {
  templateKey: TemplateKey
  name: string
  mode: StrategyMode
  budget: Budget
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

export interface Api {
  getTemplates(): Promise<StrategyTemplate[]>
  getTemplate(key: TemplateKey): Promise<TemplateDetail | undefined>
  getDashboard(): Promise<Dashboard>
  /** Starts a strategy. Rejects with `NameTakenError` when the user already runs one with that name. */
  startStrategy(strategy: NewStrategy): Promise<StrategySummary>
  /** Admin page. Each rejects with `UnauthorizedError` without the admin cookie (or a wrong password), `TooManyTriesError` when rate-limited. */
  adminLogIn(password: string): Promise<void>
  adminLogOut(): Promise<void>
  getAdminSettings(): Promise<AdminSetting[]>
  /** Rejects with `InvalidValueError` (its message is for the user) when the value is refused. */
  setAdminSetting(key: string, value: string): Promise<void>
  /** Back to the server's value or the default; `clear` leaves no value at all (secrets). */
  resetAdminSetting(key: string, clear?: boolean): Promise<void>
}
