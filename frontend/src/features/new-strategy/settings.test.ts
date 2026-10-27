import { describe, expect, it } from 'vitest'
import type { Setting, TemplateDetail } from '../../api'
import { changedSettings, formatSetting, rulesWithSettings, settingBounds, settingValues, settingsSummary, stepSetting } from './settings'

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

  it('fills rule placeholders from tested defaults and the user’s settings', () => {
    const trend = {
      defaults: { params: { smaDays: 200, bandPct: 3 } },
      settings: [
        { key: 'smaDays', sentence: '', value: '{n}', step: 10, min: 50, max: 300 },
        { key: 'bandPct', sentence: '', value: '{n}', step: 1, min: 0, max: 10 },
      ],
      rules: [
        { marker: 'BUY', text: 'Above {bandPct}% and SMA {smaDays} over {smaDays} days.' },
        { marker: 'SELL', text: 'Below {bandPct}%.' },
      ],
    } as unknown as TemplateDetail

    expect(rulesWithSettings(trend, { smaDays: 150, bandPct: 4 })).toEqual([
      { marker: 'BUY', text: 'Above 4% and SMA 150 over 150 days.' },
      { marker: 'SELL', text: 'Below 4%.' },
    ])
    expect(rulesWithSettings(trend, {})).toEqual([
      { marker: 'BUY', text: 'Above 3% and SMA 200 over 200 days.' },
      { marker: 'SELL', text: 'Below 3%.' },
    ])
  })

  it('steps a decimal setting without float drift', () => {
    const ratio: Setting = { key: 'volumeRatio', sentence: '', value: '{n}×', step: 0.1, min: 1, max: 3 }
    expect(stepSetting(ratio, 1.2, 1)).toBe(1.3)
    expect(stepSetting(ratio, 1.3, -1)).toBe(1.2)
    expect(stepSetting(peak, 60, -1)).toBe(55)
  })
})
