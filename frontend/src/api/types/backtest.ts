/** The backtest's data, for the owner page /admin/backtest-data: backend Backtest/BacktestDataReport.cs and BacktestDataEndpoints.cs. */

export type MissingReason = 'noSymbol' | 'noPrices' | 'nameDiffers' | 'noSector' | 'startsLate' | 'noPricesAtStart' | 'otherClass'

/** A member that can't be drawn, in the periods (labels) where it was a member. */
export interface MissingMember {
  ticker: string
  symbol: string | null
  periods: string[]
  reason: MissingReason
  detail: string
}

/** A ticker the symbol map sent to another Yahoo symbol, or to none. */
export interface RenamedTicker {
  ticker: string
  symbol: string | null
  note: string
}

/** A member drawn without a name check: Wikipedia doesn't name it and no symbol-map row vouches for it. */
export interface UncheckedName {
  ticker: string
  symbol: string
  yahooName: string | null
  periods: string[]
}

/** A drafted stock: third 1 is the low size third, 3 the high one. */
export interface DraftStock {
  ticker: string
  symbol: string
  sector: string
  third: 1 | 2 | 3
}

/** A drafted stock that gave up its seat: it left the index, or is still in it but can't be drawn (`stillInIndex`). */
export interface DraftReplacement {
  left: DraftStock
  replacement: DraftStock | null
  stillInIndex: boolean
}

export interface DrawPeriodReport {
  label: string
  start: string
  end: string
  partial: boolean
  /** fja's members on the start date. */
  members: number
  /** The members that can be drawn (prices from the warm-up on), one per Yahoo symbol. */
  eligible: number
  secondList: { members: number; wikipediaMembers: number; onlyFja: string[]; onlyWikipedia: string[] }
  sectors: { sector: string; eligible: number; seats: number }[]
  draft: DraftStock[]
  kept: number
  replaced: DraftReplacement[]
  notes: string[]
  /** Same stocks, monthly, as yearly money-weighted rates (fractions). */
  representative: { draftRate: number | null; allRate: number | null; passed: boolean }
}

/** One stock template in one year on the draft, before the budget's limits. */
export interface CandidateYear {
  template: string
  year: number
  candidates: number
  signalled: number
  buySignals: number
  passed: boolean
}

export type PriceWarningKind = 'gap' | 'jump' | 'stops'

export interface PriceWarning {
  symbol: string
  kind: PriceWarningKind
  date: string
  detail: string
}

export interface DrawReport {
  drawnAt: string
  seed: number
  size: number
  membershipHash: string
  membershipLastRow: string
  periods: DrawPeriodReport[]
  missing: MissingMember[]
  renamed: RenamedTicker[]
  unchecked: UncheckedName[]
  candidates: CandidateYear[]
  warnings: PriceWarning[]
  /** The other drawable members' one-day moves over 40%: they count in the representative check. */
  memberJumps?: PriceWarning[]
  /** How many Yahoo symbols were asked. */
  symbols: number
  /** The snapshot folder the draw wrote. */
  snapshot: string
}

export type SnapshotRole = 'draft' | 'index' | 'starter'

export interface SnapshotTicker {
  symbol: string
  name: string | null
  roles: SnapshotRole[]
  first: string | null
  last: string | null
  days: number
  problem: string | null
}

export interface SnapshotReport {
  month: string
  takenAt: string
  key: string
  drawnAt: string
  membershipHash: string
  tickers: SnapshotTicker[]
  warnings: PriceWarning[]
}

export type BacktestJobKind = 'draw' | 'snapshot'

export interface BacktestDataStatus {
  /** Whether the server has object storage: without it nothing can be drawn. */
  storage: boolean
  running: { kind: BacktestJobKind; started: string; progress: { step: string; done: number; total: number } | null } | null
  last: { kind: BacktestJobKind; started: string; finished: string; error: string | null } | null
  membership: { hash: string; lastRow: string; changedSinceDraw: boolean }
  /** A new year moved the periods since the draw: draw again. */
  periodsChangedSinceDraw: boolean
  draw: DrawReport | null
  snapshot: SnapshotReport | null
}

/** Which stocks a test uses: the test draft, the strategy's list, or one stock. */
export type TestStocks = 'draft' | 'list' | 'stock'

/** A test to run (backend Backtest/BacktestRunner.cs): anything left out takes the test defaults. */
export interface BacktestRequest {
  templateKey: string
  /** Settings over the template's defaults in use, by key (checked as a strategy's are); left out: the defaults. */
  settings?: Record<string, number>
  stocks?: TestStocks
  symbols?: string[]
  /** The fund Bear markets and Index trend trade; left out: SPY. */
  index?: string
  years?: number
  periodYears?: number
  budget?: number
  amountPerTrade?: number
  maxTrades?: number
  monthlyLimit?: number
  feePct?: number
  spreadPct?: number
  currency?: string
}

/** The test as run, with its defaults filled in. `amountPerTrade` is null for all in (Index trend). */
export interface BacktestSetup {
  templateKey: string
  params: Record<string, unknown>
  stocks: TestStocks
  symbols: string[]
  index: string
  years: number
  periodYears: number
  budget: number
  amountPerTrade: number | null
  maxTrades: number
  monthlyLimit: number
  feePct: number
  spreadPct: number
  currency: string
}

export interface BacktestPeriod {
  firstYear: number
  lastYear: number
  start: string
  end: string
  warmUpStart: string
  partial: boolean
  label: string
}

export interface YearReturn {
  year: number
  /** A fraction: −0.12 is −12%. */
  return: number
}

export interface ValuePoint {
  date: string
  invested: number
  value: number
}

/** One line in one period: rates are fractions (0.116 is +11.6% a year), `budgetAtWork` 0–1. */
export interface LineResult {
  rate: number | null
  years: YearReturn[]
  budgetAtWork: number
  trades: number
  invested: number
  endValue: number
  values: ValuePoint[]
  worst: YearReturn | null
}

export interface BacktestTrade {
  date: string
  symbol: string
  side: 'buy' | 'sell'
  stepNo: number | null
  maxSteps: number | null
  /** The open, in the stock's currency. */
  price: number
  shares: number
  /** In the test's currency: what went in (a buy) or came back (a sale). */
  amount: number
  costs: number
  fxRate: number
  signalDate: string | null
}

export interface BacktestPeriodResult {
  period: BacktestPeriod
  stocks: number
  strategy: LineResult
  index: LineResult
  /** Null for a pool of one: it would be the strategy itself. */
  sameDays: LineResult | null
  sameStocks: LineResult
  trades: BacktestTrade[]
  unpaid: number
  satOut: string[]
}

/** All: the average of the periods' rates, the worst year of any, the average budget at work, the trades added up. */
export interface AllLine {
  rate: number | null
  worst: YearReturn | null
  budgetAtWork: number
  trades: number
}

export interface BacktestRun {
  key: string
  ranAt: string
  /** The snapshot's month. */
  snapshot: string
  setup: BacktestSetup
  dataFrom: string
  dataTo: string
  pools: { label: string; stocks: string[]; satOut: string[] }[]
  result: {
    periods: BacktestPeriodResult[]
    strategy: AllLine
    index: AllLine
    sameDays: AllLine | null
    sameStocks: AllLine
  }
}
