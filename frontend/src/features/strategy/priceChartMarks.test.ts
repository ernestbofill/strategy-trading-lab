import { describe, expect, it } from 'vitest'
import type { RuleCheck } from '../../api'
import { chartMarks, chartSeries, lastDays, segments, spreadLabels } from './priceChartMarks'

const check = (fields: Partial<RuleCheck> & Pick<RuleCheck, 'key' | 'group' | 'met'>): RuleCheck => ({
  value: null, min: null, max: null, level: null, progress: null, used: null, ...fields,
})

describe('chartMarks', () => {
  it('shows the comeback its beaten-down line while not a candidate', () => {
    const marks = chartMarks([
      check({ key: 'beaten', group: 'candidate', met: false, min: 30, max: 60 }),
      check({ key: 'drop', group: 'candidate', met: false, min: 30, level: 300 }),
    ])
    expect(marks).toEqual([{ id: 'drop', label: 'stock.chart.beaten', tone: 'watch', from: 210 }])
    // A candidate today draws its buy area on every day, never this line.
    expect(chartMarks([check({ key: 'beaten', group: 'candidate', met: false }), check({ key: 'drop', group: 'candidate', met: false, min: 30, level: 300 })], true)).toEqual([])
  })

  it('shows a bear its fall line, and its bounce line only once a drop started', () => {
    const drop = check({ key: 'drop', group: 'candidate', met: false, min: 20, level: 500 })
    // Before the start a day has only its candidate checks.
    expect(chartMarks([drop])).toEqual([{ id: 'drop', label: 'stock.chart.fall', tone: 'sell', from: 400 }])
    expect(chartMarks([drop, check({ key: 'bounce', group: 'buy', met: false, min: 5 })])).toEqual([
      { id: 'drop', label: 'stock.chart.fall', tone: 'sell', from: 400 },
    ])
    const inDrop = [{ ...drop, met: true }, check({ key: 'bounce', group: 'buy', met: false, min: 5, level: 330, endLevel: 314 })]
    expect(chartMarks(inDrop).map((m) => m.id)).toEqual(['drop', 'bounce'])
  })

  it('shows a dip its discount zone as a candidate, else its proven growth line', () => {
    const checks = [
      check({ key: 'gain', group: 'candidate', met: true, min: 50, level: 100 }),
      check({ key: 'zone', group: 'buy', met: false, min: 5, max: 15, level: 190, endLevel: 170 }),
    ]
    expect(chartMarks(checks)).toEqual([{ id: 'zone', label: 'stock.chart.zone', tone: 'buy', from: 190, to: 170 }])
    expect(chartMarks([{ ...checks[0], met: false }, checks[1]])).toEqual([{ id: 'growth', label: 'stock.chart.growth', tone: 'watch', from: 150 }])
    // A day that was a candidate, drawn for a stock that isn't one today: the growth line, not the zone.
    expect(chartMarks(checks, false).map((m) => m.id)).toEqual(['growth'])
  })

  it("shows a held comeback stock its next milestone's buy area, not a used one", () => {
    const marks = chartMarks([
      check({ key: 'sma200', group: 'next', met: true, used: true, max: 8, level: 160, endLevel: 172.8 }),
      check({ key: 'golden', group: 'next', met: false, max: 8, level: 150, endLevel: 162 }),
    ])
    expect(marks).toEqual([{ id: 'next:golden', label: null, check: 'golden', tone: 'buy', from: 150, to: 162 }])
  })

  it("shows the index trend's buy and sell lines", () => {
    const marks = chartMarks([check({ key: 'sellLine', group: 'sell', met: false, level: 95, midLevel: 100, endLevel: 105 })])
    expect(marks.map((m) => [m.id, m.from])).toEqual([['buyLine', 105], ['sellLine', 95]])
  })
})

describe('chartSeries', () => {
  it('follows a level day by day, in pieces where it stops', () => {
    const beaten = (level: number) => [check({ key: 'beaten', group: 'candidate', met: false }), check({ key: 'drop', group: 'candidate', met: false, min: 25, level })]
    const days = [beaten(200), beaten(240), [], beaten(220)].map((checks, i) => ({ date: `2026-10-0${i + 1}`, close: 100, checks }))
    const [series] = chartSeries(days, false)
    expect(series.points.map((p) => [p.i, p.from])).toEqual([[0, 150], [1, 180], [3, 165]])
    expect(segments(series.points).map((run) => run.length)).toEqual([2, 1])
  })
})

describe('lastDays', () => {
  it('keeps the calendar days up to the latest close', () => {
    const points = ['2026-09-01', '2026-10-24', '2026-10-30'].map((date) => ({ date, close: 1 }))
    expect(lastDays(points, 7).map((p) => p.date)).toEqual(['2026-10-24', '2026-10-30'])
  })
})

describe('spreadLabels', () => {
  it('pushes overlapping labels apart and keeps them in the plot', () => {
    expect(spreadLabels([50, 52, 150], 16, 8, 152)).toEqual([50, 66, 150])
    expect(spreadLabels([150, 151], 16, 8, 152)).toEqual([136, 152])
  })
})
