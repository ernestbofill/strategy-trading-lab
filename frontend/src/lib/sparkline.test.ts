import { describe, expect, it } from 'vitest'
import { chartLines, sparklinePoints } from './sparkline'

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

describe('chartLines', () => {
  it('puts every line on the same scale', () => {
    expect(chartLines([[0, 10], [0, 5]], 100, 40)).toEqual(['0,38 100,2', '0,38 100,20'])
  })
  it('leaves out the days a line has no value', () => {
    expect(chartLines([[0, 5, 10], [null, 5, null]], 100, 40)).toEqual(['0,38 50,20 100,2', '50,20'])
  })
  it('draws nothing for fewer than two days', () => {
    expect(chartLines([[1], [2]], 100, 40)).toEqual(['', ''])
  })
})
