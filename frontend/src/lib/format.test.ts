import { describe, expect, it } from 'vitest'
import { isoDate, money, shortDate, signedPct } from './format'

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

describe('money', () => {
  it('writes the symbol of the currency before the amount', () => {
    expect(money(3000, 'EUR')).toBe('€3,000')
    expect(money(3000, 'USD')).toBe('$3,000')
    expect(money(1250000, 'GBP')).toBe('£1,250,000')
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
