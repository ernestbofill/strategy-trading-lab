import type { AllLine, BacktestRun, LineResult, YearReturn } from '../../api'

/** One row of the results table: a line's yearly rate, worst year and budget at work, as percentages. */
export interface LineRow {
  key: 'strategy' | 'index' | 'sameDays' | 'sameStocks'
  rate: number | null
  worst: YearReturn | null
  /** 0–100. */
  atWork: number
  trades: number
}

/** The tab "All" or a period's label. */
export const ALL = 'all'

const row = (key: LineRow['key'], line: LineResult | AllLine): LineRow => ({
  key,
  rate: line.rate === null ? null : line.rate * 100,
  worst: line.worst,
  atWork: line.budgetAtWork * 100,
  trades: line.trades,
})

/**
 * The table's rows for a tab: the strategy, then baselines 1, 2 and 3. Baseline 2 is left out where it would be the strategy
 * itself (one stock, or one fund).
 */
export function lineRows(run: BacktestRun, tab: string): LineRow[] {
  const lines =
    tab === ALL
      ? run.result
      : run.result.periods.find((p) => p.period.label === tab) ?? run.result.periods[run.result.periods.length - 1]
  return [
    row('strategy', lines.strategy),
    row('index', lines.index),
    ...(lines.sameDays ? [row('sameDays', lines.sameDays)] : []),
    row('sameStocks', lines.sameStocks),
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
