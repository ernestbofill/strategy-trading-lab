// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type StrategyDetail, type TemplateDetail } from '../../api'
import { StrategySettings } from './StrategySettings'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const template = {
  key: 'dip',
  name: 'Winners at a discount',
  tagline: 'Buy dips in winning stocks',
  defaultsVersion: 1,
  defaults: {
    version: 1,
    budget: { amount: 3000, step: 100, maxSteps: 5 },
    params: { zoneStartPct: 5, zoneEndPct: 6, peakDays: 60 },
  },
  settings: [
    { key: 'zoneStartPct', step: 1, min: 2, max: 20, below: 'zoneEndPct', sentence: 'Buy when {value} below peak', value: '{n}%' },
    { key: 'zoneEndPct', step: 1, min: 3, max: 30, sentence: 'Down to {value} below peak', value: '{n}%' },
    { key: 'peakDays', step: 10, min: 20, max: 120, sentence: 'Peak of last {value}', value: '{n} days' },
  ],
  settingGroups: [],
  backtest: {
    periods: [{ period: '2020-2024', template: 120, index: 80, worstYear: -10 }],
    yearly: [],
  },
  rules: [],
  howTested: { summary: '', periods: [] },
} as unknown as TemplateDetail

const strategyDetail: StrategyDetail = {
  id: 's1',
  name: 'Dip Strategy',
  templateKey: 'dip',
  templateName: 'Winners at a discount',
  mode: 'live',
  status: 'running',
  endedOn: null,
  archived: false,
  notifications: true,
  startedOn: '2026-10-13',
  today: '2026-10-24',
  currency: 'EUR',
  budget: { amount: 3000, used: 1200, step: 100, maxSteps: 5, steps: 12, totalSteps: 30 },
  results: null,
  pastResult: null,
  signals: null,
  words: null,
  stocks: [],
  trades: [],
  signalHistory: [],
  rates: [],
  settings: { peakDays: 80 },
}

describe('StrategySettings', () => {
  it('loads strategy settings, lets user tune and save them', async () => {
    vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
    vi.spyOn(api, 'getStrategy').mockResolvedValue(strategyDetail)
    vi.spyOn(api, 'getTemplate').mockResolvedValue(template)
    const updateSpy = vi.spyOn(api, 'updateStrategySettings').mockResolvedValue()

    render(
      <MemoryRouter initialEntries={['/strategies/s1/settings']}>
        <Routes>
          <Route path="/strategies/:id/settings" element={<StrategySettings />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByRole('heading', { name: 'Advanced settings' })).toBeDefined()
    expect(screen.getByText('80 days')).toBeDefined()

    // Increase peakDays from 80 to 90
    const increaseButtons = screen.getAllByRole('button', { name: 'Increase' })
    fireEvent.click(increaseButtons[2])
    expect(screen.getByText('90 days')).toBeDefined()

    // Click Save settings
    fireEvent.click(screen.getByRole('button', { name: 'Save settings' }))

    expect(updateSpy).toHaveBeenCalledWith('s1', expect.objectContaining({ peakDays: 90 }))
  })

  it('resets to tested defaults when Reset button is clicked', async () => {
    vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
    vi.spyOn(api, 'getStrategy').mockResolvedValue(strategyDetail)
    vi.spyOn(api, 'getTemplate').mockResolvedValue(template)

    render(
      <MemoryRouter initialEntries={['/strategies/s1/settings']}>
        <Routes>
          <Route path="/strategies/:id/settings" element={<StrategySettings />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('80 days')).toBeDefined()

    const resetBtn = screen.getByRole('button', { name: /Reset to tested/i }) as HTMLButtonElement
    expect(resetBtn.disabled).toBe(false)
    fireEvent.click(resetBtn)

    expect(screen.getByText('60 days')).toBeDefined()
  })
})
