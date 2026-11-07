import { describe, expect, it } from 'vitest'
import type { RuleCheck } from '../../api'
import { chartMarks, lastDays } from './priceChartMarks'

const check = (fields: Partial<RuleCheck> & Pick<RuleCheck, 'key' | 'group' | 'met'>): RuleCheck => ({
  value: null, min: null, max: null, level: null, progress: null, used: null, ...fields,
})

describe('chartMarks', () => {
  it('shows a bear its fall line and, before a drop, the bounce off that line', () => {
    const marks = chartMarks(
      [check({ key: 'drop', group: 'candidate', met: false, min: 20, level: 500 }), check({ key: 'bounce', group: 'buy', met: false, min: 5 })],
      false,
    )
    expect(marks).toEqual([
      { label: 'stock.chart.fall', tone: 'sell', from: 400 },
      { label: 'stock.chart.bounce', tone: 'buy', from: 420 },
    ])
  })

  it('shows a dip its discount zone as a candidate, else its proven growth line', () => {
    const checks = [
      check({ key: 'gain', group: 'candidate', met: false, min: 50, level: 100 }),
      check({ key: 'zone', group: 'buy', met: false, min: 5, max: 15, level: 190, endLevel: 170 }),
    ]
    expect(chartMarks(checks, true)).toEqual([{ label: 'stock.chart.zone', tone: 'buy', from: 190, to: 170 }])
    expect(chartMarks(checks, false)).toEqual([{ label: 'stock.chart.growth', tone: 'watch', from: 150 }])
  })

  it("shows the index trend's buy and sell lines", () => {
    const marks = chartMarks([check({ key: 'sellLine', group: 'sell', met: false, level: 95, midLevel: 100, endLevel: 105 })], true)
    expect(marks.map((m) => [m.label, m.from])).toEqual([['stock.chart.buyLine', 105], ['stock.chart.sellLine', 95]])
  })
})

describe('lastDays', () => {
  it('keeps the calendar days up to the latest close', () => {
    const points = ['2026-09-01', '2026-10-24', '2026-10-30'].map((date) => ({ date, close: 1 }))
    expect(lastDays(points, 7).map((p) => p.date)).toEqual(['2026-10-24', '2026-10-30'])
  })
})
