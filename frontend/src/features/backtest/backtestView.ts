import type { AllLine, BacktestDefaults, BacktestRequest, BacktestRun, BacktestSetup, LineResult, StrategyDetail, TemplateDetail, YearReturn } from '../../api'
import { followedStocks } from '../strategy/stockLine'

/** One row of the results table: a line's yearly rate, worst year, budget at work and budget used, as percentages. */
export interface LineRow {
  key: 'strategy' | 'index' | 'sameDays' | 'sameStocks'
  rate: number | null
  worst: YearReturn | null
  /** 0–100. */
  atWork: number
  /** 0–100: the budget's new money put in over the period (sale proceeds going back in aren't new money); All averages the periods. */
  used: number
  trades: number
}

/** The tab "All" or a period's label. */
export const ALL = 'all'

const row = (key: LineRow['key'], line: LineResult | AllLine, used: number): LineRow => ({
  key,
  rate: line.rate === null ? null : line.rate * 100,
  worst: line.worst,
  atWork: line.budgetAtWork * 100,
  used,
  trades: line.trades,
})

/**
 * The table's rows for a tab: the strategy, then baselines 1, 2 and 3. Baseline 2 is left out where it would be the strategy
 * itself (one stock, or one fund).
 */
export function lineRows(run: BacktestRun, tab: string): LineRow[] {
  const periods = tab === ALL ? run.result.periods : [run.result.periods.find((p) => p.period.label === tab) ?? run.result.periods[run.result.periods.length - 1]]
  const lines = tab === ALL ? run.result : periods[0]
  const budget = run.setup.budget
  // The share of the budget put in, per period, averaged for All as the budget at work is.
  const used = (key: LineRow['key']) =>
    budget > 0 ? (periods.reduce((sum, p) => sum + (p[key]?.invested ?? 0), 0) / periods.length / budget) * 100 : 0
  return [
    row('strategy', lines.strategy, used('strategy')),
    row('index', lines.index, used('index')),
    ...(lines.sameDays ? [row('sameDays', lines.sameDays, used('sameDays'))] : []),
    row('sameStocks', lines.sameStocks, used('sameStocks')),
  ]
}

/** What the gaps say (docs/backtest.md): strategy vs 1, the market; vs 2, the choice of stocks; 2 vs 3, the timing. Points a year. */
export function gaps(rows: LineRow[]): { key: 'market' | 'choice' | 'timing'; points: number }[] {
  const rate = (key: LineRow['key']) => rows.find((r) => r.key === key)?.rate ?? null
  const gap = (a: number | null, b: number | null) => (a === null || b === null ? null : a - b)
  const all = [
    { key: 'market' as const, points: gap(rate('strategy'), rate('index')) },
    { key: 'choice' as const, points: gap(rate('strategy'), rate('sameDays')) },
    { key: 'timing' as const, points: gap(rate('sameDays'), rate('sameStocks')) },
  ]
  return all.filter((g): g is { key: typeof g.key; points: number } => g.points !== null)
}

/** The trades of a tab: a period's, or every period's for All. */
export const tradesOf = (run: BacktestRun, tab: string) =>
  tab === ALL ? run.result.periods.flatMap((p) => p.trades) : run.result.periods.find((p) => p.period.label === tab)?.trades ?? []

/** The value lines of a period for the chart: the strategy and the index fund, value over money in. */
export function chartSeries(run: BacktestRun, tab: string) {
  const period = run.result.periods.find((p) => p.period.label === tab) ?? run.result.periods[run.result.periods.length - 1]
  const gain = (line: LineResult) => line.values.map((v) => (v.invested > 0 ? (v.value / v.invested - 1) * 100 : null))
  return { strategy: gain(period.strategy), index: gain(period.index), dates: period.strategy.values.map((v) => v.date) }
}

/**
 * The page's opening test, from where it was opened: a template (its defaults, or the settings on screen), a strategy (its settings,
 * budget, monthly limit and currency, and a fund template's own fund, then the test settings saved for it unless `saved` is false)
 * or a stock (the saved settings left out: one stock has its own budget and years).
 */
export function preset(
  template: TemplateDetail,
  strategy: StrategyDetail | null,
  stock: string | null,
  settings: Record<string, number> | undefined,
  saved = true,
): BacktestRequest {
  const request: BacktestRequest = { templateKey: template.key }
  const fund = template.investsIn !== 'stockList'
  const params = settings ?? strategy?.settings ?? undefined
  if (params && Object.keys(params).length > 0) request.settings = params
  if (strategy) {
    // A strategy's test uses its own budget and monthly limit (none: the budget).
    request.budget = strategy.budget.amount
    request.monthlyLimit = strategy.budget.monthlyLimit ?? strategy.budget.amount
    if (strategy.budget.step !== null) request.amountPerTrade = strategy.budget.step
    if (strategy.budget.maxSteps !== null) request.maxTrades = strategy.budget.maxSteps
    request.currency = strategy.currency
    // A fund template tests the fund the strategy trades, not SPY.
    const own = followedStocks(strategy)[0]
    if (fund && own) request.index = own.symbol
    const defaults = saved && !stock ? strategy.backtestDefaults : null
    if (defaults) {
      for (const key of DEFAULT_KEYS) {
        const value = defaults[key]
        if (value !== undefined && value !== null) Object.assign(request, { [key]: value })
      }
      // A list tests the strategy's stocks as they are now.
      if (defaults.stocks === 'list') request.symbols = strategy.stocks.map((s) => s.symbol)
    }
  }
  if (stock) {
    request.stocks = 'stock'
    request.symbols = [stock]
    delete request.budget
  }
  return request
}

/** Every key of `BacktestDefaults`: a record, so the compiler says when the type gains one this list lacks. */
const DEFAULT_KEYS = Object.keys({
  stocks: true, symbols: true, index: true, years: true, periodYears: true, budget: true, amountPerTrade: true, maxTrades: true,
  monthlyLimit: true, feePct: true, spreadPct: true,
} satisfies Record<keyof BacktestDefaults, true>) as (keyof BacktestDefaults)[]

/** The test settings to save as a strategy's defaults from a test as run: its stocks (a list's are the strategy's), years, money and costs. */
export function defaultsOf(setup: BacktestSetup, fund: boolean): BacktestDefaults {
  // The stocks always: the server reads a left-out one as the test draft (a fund's test is its one fund, whatever this says).
  const defaults: BacktestDefaults = {
    stocks: setup.stocks,
    years: setup.years,
    periodYears: setup.periodYears,
    budget: setup.budget,
    monthlyLimit: setup.monthlyLimit,
    feePct: setup.feePct,
    spreadPct: setup.spreadPct,
  }
  if (fund) defaults.index = setup.index
  else if (setup.stocks === 'stock') defaults.symbols = setup.symbols
  if (setup.amountPerTrade !== null) {
    defaults.amountPerTrade = setup.amountPerTrade
    defaults.maxTrades = setup.maxTrades
  }
  return defaults
}

/** Whether two sets of test settings are the same (the server sends a left-out one as null). */
export const sameDefaults = (a: BacktestDefaults, b: BacktestDefaults | null | undefined) =>
  !!b && DEFAULT_KEYS.every((key) => JSON.stringify(a[key] ?? null) === JSON.stringify(b[key] ?? null))
