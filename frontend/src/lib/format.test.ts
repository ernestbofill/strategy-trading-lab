import { describe, expect, it } from 'vitest'
import { formatRate, formatShares, isoDate, money, shortDate, signedPct } from './format'

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
  it('shows at most 3 decimals', () => {
    expect(signedPct(1.23456)).toBe('+1.235%')
  })
})

describe('formatRate', () => {
  it('shows at most 3 decimals', () => {
    expect(formatRate(1.11308992)).toBe('1.113')
    expect(formatRate(0.85)).toBe('0.85')
    expect(formatRate(2)).toBe('2')
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

describe('formatShares', () => {
  it('rounds to at most 3 decimals, and a holding under one share to 3 significant digits', () => {
    expect(formatShares(2.609108)).toBe('2.609')
    expect(formatShares(0.0005)).toBe('0.0005')
    expect(formatShares(0.00142857)).toBe('0.00143')
    expect(formatShares(0.5)).toBe('0.5')
    expect(formatShares(0)).toBe('0')
    expect(formatShares(12)).toBe('12')
    expect(formatShares(1250.5)).toBe('1,250.5')
  })
})
