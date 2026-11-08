import { describe, expect, it } from 'vitest'
import { timeZoneLabel, timeZones } from './timeZones'

describe('timeZones', () => {
  it('always offers the current one, once', () => {
    const zones = timeZones('Europe/Madrid')
    expect(zones.filter((z) => z === 'Europe/Madrid')).toHaveLength(1)
    expect(timeZones('Not/AZone')[0]).toBe('Not/AZone')
  })

  it('names Spain after Barcelona and keeps Madrid as the value', () => {
    expect(timeZoneLabel('Europe/Madrid')).toMatch(/^Europe\/Barcelona/)
    expect(timeZoneLabel('America/New_York')).toMatch(/^America\/New York/)
  })
})
