// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type TemplateDetail } from '../../api'
import { Advanced } from './Advanced'
import { NewStrategyDraftProvider, useNewStrategyDraft } from './NewStrategyDraft'

afterEach(cleanup)

const dip = {
  key: 'dip',
  name: 'Winners at a discount',
  backtest: { periods: [{ period: 'All', template: 212, index: 248, worstYear: -21 }] },
  defaults: { version: 1, budget: { amount: 3000, step: 100, maxSteps: 5 }, params: { zoneStartPct: 5, zoneEndPct: 6, peakDays: 60 } },
  settings: [
    { key: 'zoneStartPct', sentence: 'The discount zone starts {value} below the recent peak.', value: '{n}%', step: 1, min: 1, max: 39, below: 'zoneEndPct', group: 'signal', help: 'The smallest dip that counts.' },
    { key: 'zoneEndPct', sentence: 'It ends {value} below the recent peak.', value: '{n}%', step: 1, min: 2, max: 40, group: 'signal' },
    { key: 'peakDays', sentence: 'The recent peak is the highest price of the last {value}.', value: '{n} days', step: 5, min: 20, max: 60, group: 'signal' },
  ],
  settingGroups: [{ key: 'signal', title: '2 · Buy signal', lead: 'A candidate signals in the zone, above the `SMA 50`.' }],
} as unknown as TemplateDetail

const trend = {
  key: 'trend',
  name: 'Index trend',
  backtest: { periods: [{ period: 'All', template: 249, index: 272, worstYear: -11 }] },
  defaults: { version: 1, budget: { amount: 3000, step: null, maxSteps: null }, params: { smaDays: 200, bandPct: 3 } },
  settings: [
    { key: 'smaDays', sentence: 'The trend line is the average price over {value}.', value: '{n} days', step: 10, min: 50, max: 300 },
    { key: 'bandPct', sentence: 'The buy and sell lines are {value} above and below the trend line.', value: '{n}%', step: 1, min: 0, max: 10 },
  ],
  settingGroups: [],
} as unknown as TemplateDetail

/** The screen it returns to: shows what the draft kept. */
function Review() {
  const { settings } = useNewStrategyDraft()
  return <p>Saved {JSON.stringify(settings)}</p>
}

function open(template: TemplateDetail = dip) {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getTemplate').mockResolvedValue(template)
  render(
    <NewStrategyDraftProvider owner="a@example.com">
      <MemoryRouter initialEntries={[`/new/templates/${template.key}`, `/new/templates/${template.key}/advanced`]} initialIndex={1}>
        <Routes>
          <Route path="/new/templates/:key" element={<Review />} />
          <Route path="/new/templates/:key/advanced" element={<Advanced />} />
        </Routes>
      </MemoryRouter>
    </NewStrategyDraftProvider>,
  )
}

const row = (sentence: RegExp) => screen.getByRole('group', { name: sentence })

describe('Advanced', () => {
  it('marks a changed setting, keeps the zone start under its end, and saves only the changes', async () => {
    open()
    expect(await screen.findByText("You're on the tested defaults. Change a setting to compare.")).toBeTruthy()

    // The zone runs 5–6%: its start can't go up, its end can't come down.
    const start = row(/The discount zone starts/)
    expect(within(start).getByRole('button', { name: 'Increase' }).hasAttribute('disabled')).toBe(true)
    expect(within(row(/It ends/)).getByRole('button', { name: 'Decrease' }).hasAttribute('disabled')).toBe(true)
    // The peak is at its maximum.
    const peak = row(/The recent peak/)
    expect(within(peak).getByRole('button', { name: 'Increase' }).hasAttribute('disabled')).toBe(true)

    fireEvent.click(within(peak).getByRole('button', { name: 'Decrease' }))
    expect(within(peak).getByText('55 days')).toBeTruthy()
    expect(within(peak).getByText('CHANGED')).toBeTruthy()
    expect(within(peak).getByText('Tested: 60 days')).toBeTruthy()
    expect(screen.getByRole('table')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Save settings' }))
    expect(await screen.findByText('Saved {"peakDays":55}')).toBeTruthy()
  })

  it('shows each group with what it decides, and what a setting changes', async () => {
    open()
    const group = await screen.findByRole('region', { name: '2 · Buy signal' })
    expect(within(group).getByText('SMA 50')).toBeTruthy()
    expect(within(group).getAllByRole('group')).toHaveLength(3)
    expect(within(row(/The discount zone starts/)).getByText('The smallest dip that counts.')).toBeTruthy()
  })

  it('puts every value back with Reset to tested', async () => {
    open()
    const peak = await screen.findByRole('group', { name: /The recent peak/ })
    fireEvent.click(within(peak).getByRole('button', { name: 'Decrease' }))
    fireEvent.click(screen.getByRole('button', { name: '⟳ Reset to tested' }))

    expect(within(peak).getByText('60 days')).toBeTruthy()
    expect(within(peak).queryByText('CHANGED')).toBeNull()
  })

  it('shows Index trend settings and saves their changes', async () => {
    open(trend)

    const average = await screen.findByRole('group', { name: /The trend line is/ })
    expect(within(average).getByText('200 days')).toBeTruthy()
    fireEvent.click(within(average).getByRole('button', { name: 'Decrease' }))

    const band = row(/The buy and sell lines/)
    expect(within(band).getByText('Tested: 3%')).toBeTruthy()
    fireEvent.click(within(band).getByRole('button', { name: 'Increase' }))

    fireEvent.click(screen.getByRole('button', { name: 'Save settings' }))
    expect(await screen.findByText('Saved {"smaDays":190,"bandPct":4}')).toBeTruthy()
  })
})
