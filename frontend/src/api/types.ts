// DTOs shared with the backend. Shapes mirror the future .NET API (camelCase JSON).

export type TemplateKey = 'dip' | 'comeback' | 'drop' | 'trend' | 'ai'

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
  feePerTrade: number
}

/** Everything the review screen (2 / 5) and How we tested (2c) show. */
export interface TemplateDetail extends StrategyTemplate {
  /** "The idea" on the review screen. */
  rationale: string
  rules: Rule[]
  watches: Watches
  /** Null when the template can't be backtested. */
  backtest: Backtest | null
}

export interface Api {
  getTemplates(): Promise<StrategyTemplate[]>
  getTemplate(key: TemplateKey): Promise<TemplateDetail | undefined>
}
