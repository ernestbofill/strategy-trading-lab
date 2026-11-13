import type { ChartDay, RuleCheck } from '../../api'
import type { MessageKey } from '../../i18n/messages/en'

/**
 * A level the chart draws over the price on one day: a line, or an area between two prices. `id` ties the days of one
 * level together; `label` names it, or `check` when it takes the rule's own name (a next-step milestone). `tone` picks its colour.
 */
export interface ChartMark {
  id: string
  label: MessageKey | null
  check?: string
  /** `short` and `long` are the moving averages a goal is about (the golden cross's SMA 50 and SMA 200). */
  tone: 'buy' | 'sell' | 'watch' | 'short' | 'long'
  from: number
  to?: number
}

const below = (level: number, pct: number) => level * (1 - pct / 100)
const above = (level: number, pct: number) => level * (1 + pct / 100)

/** Whether the day's checks make the stock a candidate: every candidate check that has a verdict is met. */
export const isCandidateOn = (checks: RuleCheck[]) => checks.every((c) => c.group !== 'candidate' || c.met !== false)

/** A held stock's next step: the first milestone not used yet with a buy window (the chart draws it as an area, but the golden cross as its two averages). */
export const nextGoal = (checks: RuleCheck[]) =>
  checks.find((c) => c.group === 'next' && !c.used && !c.preview && c.level !== null && c.endLevel != null)

/**
 * One day's levels from its checks' keys (not the template's): a candidate's buy area or discount zone, else the line that
 * would make it a candidate (beaten down, proven growth); a held stock's next milestone; a bear's market fall line, and its
 * bounce line once a drop started; the index's buy and sell lines. Before the start a day has only candidate checks (and
 * next-step previews). The chart passes the stock's verdict today, so every day draws the same kind of level (Ernest,
 * 2026-11-08); likewise `goal`, today's next step: while it is the golden cross, every day draws the SMA 50 and SMA 200.
 */
export function chartMarks(checks: RuleCheck[], isCandidate = isCandidateOn(checks), goal: string | null = null): ChartMark[] {
  const marks: ChartMark[] = []
  // The comeback's `drop` comes with its `beaten` window; a bear's comes alone before the start (no bounce check then).
  const bear = !checks.some((c) => c.key === 'beaten')
  for (const c of checks) {
    switch (c.key) {
      // The comeback's turn: the buy line up to its chase limit. The index trend's band has no `max`.
      case 'buyLine':
      case 'sellLine':
        if (c.max !== null) {
          if (isCandidate && c.level !== null && c.endLevel != null)
            marks.push({ id: 'buyArea', label: 'stock.chart.buyArea', tone: 'buy', from: c.level, to: c.endLevel })
        } else if (c.level !== null && c.endLevel != null) {
          const [buy, sell] = c.key === 'buyLine' ? [c.level, c.endLevel] : [c.endLevel, c.level]
          marks.push(
            { id: 'buyLine', label: 'stock.chart.buyLine', tone: 'buy', from: buy },
            { id: 'sellLine', label: 'stock.chart.sellLine', tone: 'sell', from: sell },
          )
        }
        break
      case 'zone':
        if (isCandidate && c.level !== null && c.endLevel != null)
          marks.push({ id: 'zone', label: 'stock.chart.zone', tone: 'buy', from: c.level, to: c.endLevel })
        break
      case 'gain':
        if (!isCandidate && c.level !== null && c.min !== null)
          marks.push({ id: 'growth', label: 'stock.chart.growth', tone: 'watch', from: above(c.level, c.min) })
        break
      // The comeback has it only while not a candidate (the beaten-down line); a bear always (the market fall).
      case 'drop':
        if ((bear || !isCandidate) && c.level !== null && c.min !== null)
          marks.push({ id: 'drop', label: bear ? 'stock.chart.fall' : 'stock.chart.beaten', tone: bear ? 'sell' : 'watch', from: below(c.level, c.min) })
        break
      // Only once a drop started: before, there is no low to bounce off.
      case 'bounce':
        if (c.level !== null) marks.push({ id: 'bounce', label: 'stock.chart.bounce', tone: 'buy', from: c.level })
        break
    }
  }
  // A held stock's next step: the first milestone not used yet, from its level up to the chase limit. Not the golden cross:
  // its area sits under the price and read as a discount, so it draws the two averages instead (Ernest, 2026-11-13).
  const next = nextGoal(checks)
  if (next && next.key !== 'golden')
    marks.push({ id: `next:${next.key}`, label: null, check: next.key, tone: 'buy', from: next.level!, to: next.endLevel! })
  // The golden cross is the SMA 50 rising above the SMA 200: both lines, so the cross can be seen coming.
  const golden = goal === 'golden' ? checks.find((c) => c.key === 'golden') : undefined
  if (golden?.level != null && golden.midLevel != null)
    marks.push(
      { id: 'sma50', label: 'stock.chart.sma50', tone: 'short', from: golden.level },
      { id: 'sma200', label: 'stock.chart.sma200', tone: 'long', from: golden.midLevel },
    )
  return marks
}

/** A level across the period: its days' values by the day's index, in the order the levels first show. */
export interface ChartSeries {
  mark: Omit<ChartMark, 'from' | 'to'>
  area: boolean
  points: { i: number; from: number; to?: number }[]
}

/**
 * Each level across the days, drawn as the stock's verdict today has it: a candidate's areas, else its watch lines; and the
 * lines of today's next step (the latest day the rules checked), every day it has them.
 */
export function chartSeries(days: ChartDay[], isCandidate: boolean): ChartSeries[] {
  const byId = new Map<string, ChartSeries>()
  const latest = days.findLast((d) => d.checks.length > 0)
  const goal = latest ? (nextGoal(latest.checks)?.key ?? null) : null
  days.forEach((day, i) => {
    for (const { from, to, ...mark } of chartMarks(day.checks, isCandidate, goal)) {
      let series = byId.get(mark.id)
      if (!series) byId.set(mark.id, (series = { mark, area: to !== undefined, points: [] }))
      series.points.push({ i, from, to })
    }
  })
  return [...byId.values()]
}

/** Runs of consecutive days, so a level that stops and starts again is drawn in pieces. */
export function segments<P extends { i: number }>(points: P[]): P[][] {
  const runs: P[][] = []
  for (const p of points) {
    const run = runs[runs.length - 1]
    if (run && p.i === run[run.length - 1].i + 1) run.push(p)
    else runs.push([p])
  }
  return runs
}

/** The days of the last `days` calendar days up to the latest one. */
export function lastDays<D extends { date: string }>(points: D[], days: number): D[] {
  if (points.length === 0) return points
  const end = new Date(points[points.length - 1].date)
  end.setUTCDate(end.getUTCDate() - days)
  const from = end.toISOString().slice(0, 10)
  return points.filter((p) => p.date >= from)
}

/** The price range that holds every value, padded so lines don't sit on the edge. */
export function priceRange(values: number[]): [number, number] {
  const min = Math.min(...values)
  const max = Math.max(...values)
  const pad = (max - min) * 0.06 || max * 0.01 || 1
  return [min - pad, max + pad]
}

/**
 * Labels' positions on the right axis, in the order given: each at its own height where it can, pushed apart so none
 * overlap, and kept between `top` and `bottom`.
 */
export function spreadLabels(ys: number[], gap: number, top: number, bottom: number): number[] {
  const order = ys.map((_, k) => k).sort((a, b) => ys[a] - ys[b])
  const at = order.map((k) => Math.min(Math.max(ys[k], top), bottom))
  for (let n = 1; n < at.length; n++) at[n] = Math.max(at[n], at[n - 1] + gap)
  at[at.length - 1] = Math.min(at[at.length - 1], bottom)
  for (let n = at.length - 2; n >= 0; n--) at[n] = Math.min(at[n], at[n + 1] - gap)
  const result = [...ys]
  order.forEach((k, n) => (result[k] = at[n]))
  return result
}
