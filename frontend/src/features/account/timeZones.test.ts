import { describe, expect, it } from 'vitest'
import { timeZoneLabel, timeZoneOptions } from './timeZones'

describe('timeZones', () => {
  it('offers a short list, west to east, always with the current zone once', () => {
    const options = timeZoneOptions('Europe/Madrid')
    expect(options.length).toBeLessThan(50)
    expect(options.filter((o) => o.value === 'Europe/Madrid')).toHaveLength(1)
    expect(options[0].value).toBe('Pacific/Honolulu')
    expect(options.at(-1)?.value).toBe('Pacific/Auckland')
    // A zone off the list (guessed by a browser, or saved before it) is still there, by its own name.
    expect(timeZoneOptions('Europe/Vienna').find((o) => o.value === 'Europe/Vienna')?.label).toMatch(/· Europe\/Vienna$/)
  })

  it('names rows after cities, Spain after Barcelona first, and keeps the IANA name as the value', () => {
    const winter = new Date('2026-01-15T12:00:00Z')
    expect(timeZoneLabel('Europe/Madrid', winter)).toBe('GMT+1 · Barcelona, Madrid, Paris, Berlin, Rome')
    expect(timeZoneLabel('America/New_York', winter)).toBe('GMT-5 · New York, Toronto, Miami')
    expect(timeZoneLabel('Asia/Kolkata', winter)).toBe('GMT+5:30 · Mumbai, Delhi, Bangalore')
  })
})
