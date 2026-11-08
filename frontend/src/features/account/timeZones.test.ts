import { describe, expect, it } from 'vitest'
import { timeZones } from './timeZones'

describe('timeZones', () => {
  it('always offers the current one, once', () => {
    const zones = timeZones('Europe/Madrid')
    expect(zones.filter((z) => z === 'Europe/Madrid')).toHaveLength(1)
    expect(timeZones('Not/AZone')[0]).toBe('Not/AZone')
  })
})
