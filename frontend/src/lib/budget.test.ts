import { describe, expect, it } from 'vitest'
import { clamp, daysToUse, plural, resolveBudget, summarize } from './budget'

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
  it('counts days for AI rounds', () => expect(daysToUse(30, 14)).toBe(420))
})

describe('resolveBudget', () => {
  const none = { budget: null, step: null, maxSteps: null }

  it("starts from the template's tested defaults", () => {
    expect(resolveBudget(none, { amount: 3000, step: 600, maxSteps: 5 })).toEqual({ budget: 3000, step: 600, maxSteps: 5 })
  })
  it('keeps what the user set', () => {
    expect(resolveBudget({ ...none, step: 200 }, { amount: 3000, step: 100, maxSteps: 5 })).toEqual({
      budget: 3000,
      step: 200,
      maxSteps: 5,
    })
  })
  it('makes the whole budget one step when the template has none (all in, all out)', () => {
    expect(resolveBudget({ ...none, budget: 5000 }, { amount: 3000, step: null, maxSteps: null })).toEqual({
      budget: 5000,
      step: 5000,
      maxSteps: 1,
    })
  })
})
