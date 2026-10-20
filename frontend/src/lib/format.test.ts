import { describe, expect, it } from 'vitest'
import { isoDate, shortDate, signedPct } from './format'

describe('signedPct', () => {
  it('writes a true minus sign and a plus sign', () => {
    expect(signedPct(12)).toBe('+12%')
    expect(signedPct(-8)).toBe('−8%')
    expect(signedPct(0)).toBe('0%')
  })
  it('can show decimals', () => {
    expect(signedPct(6.4, 1)).toBe('+6.4%')
    expect(signedPct(-2, 1)).toBe('−2.0%')
  })
})

describe('dates', () => {
  it('formats a local date as a short date', () => {
    expect(shortDate('2026-10-13')).toBe('Oct 13')
  })
  it('round-trips through isoDate', () => {
    expect(isoDate(new Date(2026, 0, 5))).toBe('2026-01-05')
  })
})
