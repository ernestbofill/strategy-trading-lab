import { describe, expect, it } from 'vitest'
import type { AllLine, BacktestPeriodResult, BacktestRun, LineResult } from '../../api'
import { ALL, gaps, lineRows } from './backtestView'

const line = (rate: number): LineResult => ({ rate, years: [], budgetAtWork: 0.5, trades: 3, invested: 100, endValue: 110, values: [], worst: { year: 2024, return: -0.1 } })
const all = (rate: number): AllLine => ({ rate, worst: { year: 2022, return: -0.2 }, budgetAtWork: 0.6, trades: 9 })

const period = (label: string, sameDays: LineResult | null): BacktestPeriodResult => ({
  period: { firstYear: 2023, lastYear: 2026, start: '2023-01-01', end: '2026-11-25', warmUpStart: '2021-10-01', partial: true, label },
  stocks: 60,
  strategy: line(0.116),
  index: line(0.091),
  sameDays,
  sameStocks: line(0.098),
  trades: [],
  unpaid: 0,
  satOut: [],
})

const run = (sameDays: boolean) =>
  ({
    result: {
      periods: [period('2023–26', sameDays ? line(0.099) : null)],
      strategy: all(0.12),
      index: all(0.1),
      sameDays: sameDays ? all(0.11) : null,
      sameStocks: all(0.105),
    },
  }) as BacktestRun

describe('the backtest results', () => {
  it('reads a period as percentages, with what the gaps say', () => {
    const rows = lineRows(run(true), '2023–26')
    expect(rows.map((r) => r.key)).toEqual(['strategy', 'index', 'sameDays', 'sameStocks'])
    expect(rows[0].rate).toBeCloseTo(11.6)
    expect(rows[0].atWork).toBe(50)
    expect(gaps(rows).map((g) => [g.key, Math.round(g.points * 10) / 10])).toEqual([
      ['market', 2.5],
      ['choice', 1.7],
      ['timing', 0.1],
    ])
  })

  it('leaves out baseline 2 and its gaps for one stock or one fund, and reads All from the combined lines', () => {
    const rows = lineRows(run(false), ALL)
    expect(rows.map((r) => r.key)).toEqual(['strategy', 'index', 'sameStocks'])
    expect(rows[0].trades).toBe(9)
    expect(gaps(rows).map((g) => g.key)).toEqual(['market'])
  })
})
