import { describe, expect, it } from 'vitest'
import type { Setting, TemplateDetail } from '../../api'
import { changedSettings, formatSetting, settingBounds, settingValues, settingsSummary } from './settings'

const start: Setting = { key: 'zoneStartPct', sentence: '', value: '{n}%', step: 1, min: 1, max: 39, below: 'zoneEndPct' }
const end: Setting = { key: 'zoneEndPct', sentence: '', value: '{n}%', step: 1, min: 2, max: 40 }
const peak: Setting = { key: 'peakDays', sentence: '', value: '{n} days', step: 5, min: 20, max: 60 }
const settings = [start, end, peak]

const dip = {
  settings,
  defaults: { params: { zoneStartPct: 5, zoneEndPct: 15, peakDays: 60, cooldownDays: 10 } },
} as unknown as TemplateDetail

describe('settings', () => {
  it('reads the tested values with the user’s changes over them', () => {
    expect(settingValues(dip, { peakDays: 40 })).toEqual({ zoneStartPct: 5, zoneEndPct: 15, peakDays: 40 })
  })

  it('keeps only the values that differ from the tested ones', () => {
    expect(changedSettings(dip, { zoneStartPct: 5, zoneEndPct: 12, peakDays: 60 })).toEqual({ zoneEndPct: 12 })
  })

  it('keeps the zone’s start a step under its end, and the end a step over the start', () => {
    const values = { zoneStartPct: 8, zoneEndPct: 10, peakDays: 60 }
    expect(settingBounds(start, settings, values)).toEqual({ min: 1, max: 9 })
    expect(settingBounds(end, settings, values)).toEqual({ min: 9, max: 40 })
    expect(settingBounds(peak, settings, values)).toEqual({ min: 20, max: 60 })
  })

  it('formats a value and sums up the changes', () => {
    expect(formatSetting(peak, 45)).toBe('45 days')
    expect(settingsSummary({})).toBe('Tested defaults')
    expect(settingsSummary({ peakDays: 40, zoneEndPct: 12 })).toBe('2 changed')
  })
})
