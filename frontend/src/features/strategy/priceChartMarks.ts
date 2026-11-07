import type { PricePoint, RuleCheck } from '../../api'
import type { MessageKey } from '../../i18n/messages/en'

/** A level the chart draws over the price: a line, or an area between two prices. `tone` picks its colour. */
export interface ChartMark {
  label: MessageKey
  tone: 'buy' | 'sell' | 'watch'
  from: number
  to?: number
}

const below = (level: number, pct: number) => level * (1 - pct / 100)
const above = (level: number, pct: number) => level * (1 + pct / 100)

/**
 * The rules' levels of the latest check, to draw on the price chart, from the checks' keys (not the template's):
 * a candidate's buy area or discount zone, else the line that would make it a candidate (beaten down, proven growth);
 * a bear's market fall and bounce lines, and the index's buy and sell lines, always.
 */
export function chartMarks(checks: RuleCheck[], isCandidate: boolean): ChartMark[] {
  const marks: ChartMark[] = []
  const fall = checks.find((c) => c.key === 'drop' && c.level !== null && c.min !== null)
  const fallLine = fall ? below(fall.level!, fall.min!) : null
  for (const c of checks) {
    switch (c.key) {
      // The comeback's turn: the buy line up to its chase limit. The index trend's band has no `max`.
      case 'buyLine':
      case 'sellLine':
        if (c.max !== null) {
          if (isCandidate && c.level !== null && c.endLevel != null) marks.push({ label: 'stock.chart.buyArea', tone: 'buy', from: c.level, to: c.endLevel })
        } else if (c.level !== null && c.endLevel != null) {
          const [buy, sell] = c.key === 'buyLine' ? [c.level, c.endLevel] : [c.endLevel, c.level]
          marks.push({ label: 'stock.chart.buyLine', tone: 'buy', from: buy }, { label: 'stock.chart.sellLine', tone: 'sell', from: sell })
        }
        break
      case 'zone':
        if (isCandidate && c.level !== null && c.endLevel != null) marks.push({ label: 'stock.chart.zone', tone: 'buy', from: c.level, to: c.endLevel })
        break
      case 'gain':
        if (!isCandidate && c.level !== null && c.min !== null) marks.push({ label: 'stock.chart.growth', tone: 'watch', from: above(c.level, c.min) })
        break
      // The comeback has it only while not a candidate; a bear always.
      case 'drop':
        if (fallLine !== null) {
          const bear = checks.some((x) => x.key === 'bounce')
          marks.push({ label: bear ? 'stock.chart.fall' : 'stock.chart.beaten', tone: bear ? 'sell' : 'watch', from: fallLine })
        }
        break
      // Before a drop there is no low yet: the bounce off the fall line, to keep the distance between the two in view.
      case 'bounce': {
        const bounce = c.level ?? (fallLine !== null && c.min !== null ? above(fallLine, c.min) : null)
        if (bounce !== null) marks.push({ label: 'stock.chart.bounce', tone: 'buy', from: bounce })
        break
      }
    }
  }
  return marks
}

/** The closes of the last `days` calendar days up to the latest one. */
export function lastDays(points: PricePoint[], days: number): PricePoint[] {
  if (points.length === 0) return points
  const end = new Date(points[points.length - 1].date)
  end.setUTCDate(end.getUTCDate() - days)
  const from = end.toISOString().slice(0, 10)
  return points.filter((p) => p.date >= from)
}

/** The price range that holds the closes and every mark, padded so lines don't sit on the edge. */
export function priceRange(points: PricePoint[], marks: ChartMark[]): [number, number] {
  const values = [...points.map((p) => p.close), ...marks.flatMap((m) => (m.to === undefined ? [m.from] : [m.from, m.to]))]
  const min = Math.min(...values)
  const max = Math.max(...values)
  const pad = (max - min) * 0.06 || max * 0.01 || 1
  return [min - pad, max + pad]
}
