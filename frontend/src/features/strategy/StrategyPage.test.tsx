// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type StrategyDetail } from '../../api'
import { StrategyPage } from './StrategyPage'

afterEach(cleanup)

const detail = (fields: Partial<StrategyDetail>): StrategyDetail => ({
  id: fields.id ?? 's1',
  name: 'Zscaler & friends',
  templateKey: 'dip',
  templateName: 'Dip in winners',
  mode: 'live',
  startedOn: '2026-10-13',
  today: '2026-10-24',
  currency: 'EUR',
  budget: { amount: 3000, used: 1200, step: 100, maxSteps: 5, steps: 12, totalSteps: 30 },
  results: null,
  pastResult: { years: '2010-2024', template: 212, index: 180, worstYear: -21 },
  signals: { did: 9, skipped: 2, missed: 1 },
  words: { zone: 'the discount zone', notCandidate: 'Below its long trend' },
  stocks: [],
  ...fields,
})

function open(strategy: StrategyDetail) {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getStrategy').mockResolvedValue(strategy)
  render(
    <MemoryRouter initialEntries={[`/strategies/${strategy.id}`]}>
      <Routes>
        <Route path="/strategies/:id" element={<StrategyPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('StrategyPage', () => {
  it('shows a live strategy next to the rules alone and the index, and leads a waiting signal to the user', async () => {
    open(
      detail({
        id: 'live',
        results: { you: 4.2, rules: 5.1, index: -1.3, history: [] },
        stocks: [
          {
            assetId: 'a', symbol: 'ZS', name: 'Zscaler', status: 'waitingForYou', steps: 1, rulesSteps: 2, maxSteps: 5,
            distancePct: null, signalId: 'sig', signalStep: 2, invested: 101, result: 3.5,
          },
        ],
      }),
    )

    expect(await screen.findByRole('heading', { name: 'Zscaler & friends' })).toBeTruthy()
    expect(screen.getByText('+4.2%')).toBeTruthy()
    expect(screen.getByText('+5.1%')).toBeTruthy()
    expect(screen.getByText('−1.3%')).toBeTruthy()
    expect(screen.getByText('€1,200 of €3,000 · 12 of 30 steps')).toBeTruthy()
    expect(screen.getByText('9 did · 2 skipped · 1 missed')).toBeTruthy()
    expect(screen.getByText(/The template's worst year, 2010-2024/).textContent).toContain('−21%')
    expect(screen.getByRole('link', { name: /Zscaler WAITING FOR YOU Signal: step 2 of 5/ }).getAttribute('href')).toBe(
      '/strategies/live/signals/sig',
    )
    expect(screen.getByRole('link', { name: /Tune the settings Change its rules/i }).getAttribute('href')).toBe(
      '/strategies/live/settings',
    )
    expect(screen.getByText(/Your results come from the trades you confirmed/).textContent).toContain(
      "Past results don't promise future ones.",
    )
  })

  it("shows a simulation without trades the template's past, with its worst year", async () => {
    open(detail({ id: 'sim', mode: 'simulation', signals: null, budget: { amount: 3000, used: 0, step: 100, maxSteps: 5, steps: 0, totalSteps: 30 } }))

    expect(await screen.findByText(/No paper trades yet/)).toBeTruthy()
    expect(screen.getByText('−21%')).toBeTruthy()
    expect(screen.queryByText('Signals')).toBeNull()
    expect(screen.getByText(/Results come from paper trades/)).toBeTruthy()
  })
})
