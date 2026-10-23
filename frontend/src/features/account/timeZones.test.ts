import { describe, expect, it } from 'vitest'
import { guessTimeZone, timeZones } from './timeZones'

describe('timeZones', () => {
  it('always offers the current one, once', () => {
    const zones = timeZones('Europe/Madrid')
    expect(zones.filter((z) => z === 'Europe/Madrid')).toHaveLength(1)
    expect(timeZones('Not/AZone')[0]).toBe('Not/AZone')
  })

  it('guesses a zone', () => {
    expect(guessTimeZone()).toMatch(/\w/)
  })
})
