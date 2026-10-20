import { describe, expect, it } from 'vitest'
import { sparklinePoints } from './sparkline'

describe('sparklinePoints', () => {
  it('puts the lowest value at the bottom and the highest at the top', () => {
    expect(sparklinePoints([0, 5, 10], 100, 40)).toBe('0,38 50,20 100,2')
  })
  it('runs a flat series through the middle', () => {
    expect(sparklinePoints([3, 3], 100, 40)).toBe('0,20 100,20')
  })
  it('draws nothing for fewer than two values', () => {
    expect(sparklinePoints([3], 100, 40)).toBe('')
  })
})
