import type { Currency } from './account'
import type { AiRoundsSummary, AiSetup } from './ai'
import type { CurrencyRates, SignalStatus, StrategySignalItem, StrategyTradeItem } from './signals'
import type { Budget, InvestsIn, TemplateKey } from './templates'

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
  index: number | null
}

/** A strategy's gain since it started, in percent. */
export interface StrategyFigures {
  you: number
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
  /** Steps the results count (live: the user's buys). */
  steps: number
  maxSteps: number | null
  /** How far the price is from the rules, in percent (0 when there or past it). */
  distancePct: number | null
  /** The waiting signal (live) or today's (simulation). */
  signalId: string | null
  signalStep: number | null
  signalSide?: TradeSide | null
  /** The row's signal's status: a skipped or missed one (live) stands while its window is open. */
  signalStatus?: SignalStatus | null
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
  /** The UTC timestamp of `close` when it is a known exchange's close; null when unknown. */
  priceUpdatedAt?: string | null
  /** True when `close` was fetched after `checkedOn`'s session closed. */
  priceIsClose?: boolean
  candidateSince?: string | null
  checks: RuleCheck[]
  /** Not checked yet and left to the daily run: added while a US session is in progress. */
  afterClose?: boolean
  /** A candidate within the template's getting close % of its buy rule, as the dashboard card counts it. */
  gettingClose?: boolean
}

/** The part of the rules a check belongs to: what makes a candidate, what gives a buy or sell signal, what earns the next step. */
export type CheckGroup = 'candidate' | 'buy' | 'sell' | 'next'

/**
 * One rule's verdict on the latest checked day: backend Engine/BuyRules.cs (RuleCheck). `met` is a boolean verdict;
 * `progress` (0 to 1+, exceeding 1 past the threshold) is how far a figure that must grow has come; `used` marks a
 * next-step rule that can't earn a step any more (the comeback's turn); `preview` marks next-step rules shown before the first buy.
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

/** A day of the stock page's chart: its close and the rules' checks that day (only candidate ones and next-step previews before `startedOn`). Backend Strategies/StrategyDtos.cs (ChartDay). */
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
  /** The template's signals are years apart (Bear markets): the page says so next to the past results. */
  rareSignals?: boolean
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
