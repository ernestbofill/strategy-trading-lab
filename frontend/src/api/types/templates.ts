import type { Security } from './assets'

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
  /** "60 S&P 500 stocks, 2011–2026." */
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
  /** At most this much new money a month, unspent room carried over; null or left out: no limit. Never for all-in templates. */
  monthlyLimit?: number | null
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
