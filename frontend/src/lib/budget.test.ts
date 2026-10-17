import { describe, expect, it } from 'vitest'
import { clamp, plural, summarize, weeksToUse } from './budget'

describe('summarize', () => {
  it('matches the default example', () => {
    expect(summarize(3000, 100, 5)).toEqual({ perStock: 500, totalSteps: 30, fullStocks: 6, shown: 30 })
  })
  it('rounds down and caps the squares at 60', () => {
    expect(summarize(10_000, 30, 7)).toMatchObject({ totalSteps: 333, fullStocks: 47, shown: 60 })
  })
})

describe('helpers', () => {
  it('clamps', () => expect([clamp(0, 1, 5), clamp(9, 1, 5), clamp(3, 1, 5)]).toEqual([1, 5, 3]))
  it('uses singular for one', () => {
    expect(plural(1, 'step', 'steps')).toBe('1 step')
    expect(plural(2, 'stock', 'stocks')).toBe('2 stocks')
  })
  it('counts weeks for AI rounds', () => expect(weeksToUse(30, 2)).toBe(60))
})
