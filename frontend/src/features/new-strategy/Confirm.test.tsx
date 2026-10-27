// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { useEffect } from 'react'
import { MemoryRouter, Route, Routes, useNavigate } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type Dashboard, type StrategySummary, type TemplateDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { AccountProvider } from '../account/AccountContext'
import { Confirm } from './Confirm'
import { NewStrategyDraftProvider, useNewStrategyDraft } from './NewStrategyDraft'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const ai = {
  key: 'ai',
  name: "Your AI's top picks",
  signals: 'buys',
  watches: { kind: 'stocks', starterList: [{ id: 'id-ZS', symbol: 'ZS', name: 'Zscaler', what: '' }] },
  defaults: { version: 1, budget: { amount: 3000, step: 100, maxSteps: 5 }, params: {} },
  settings: [],
  aiRound: { everyWeeks: 2, topN: 1 },
} as unknown as TemplateDetail

/** Sets the draft to the AI template, as the earlier screens do. */
function ChooseAi() {
  const { switchTemplate } = useNewStrategyDraft()
  useEffect(() => switchTemplate('ai'), [switchTemplate])
  return null
}

function Dashboard() {
  const navigate = useNavigate()
  return <button onClick={() => navigate(-1)}>Dashboard back</button>
}

function open() {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getAccount').mockResolvedValue({
    email: 'a@example.com', emailVerified: true, locale: 'en', timeZone: 'UTC', overviewEmail: true, currency: 'EUR', role: 'user', aiSharing: false,
  })
  vi.spyOn(api, 'getTemplate').mockResolvedValue(ai)
  vi.spyOn(api, 'getDashboard').mockResolvedValue({ strategies: [], waiting: [] } as unknown as Dashboard)
  render(
    <AccountProvider>
      <NewStrategyDraftProvider owner="a@example.com">
        <ChooseAi />
        <MemoryRouter initialEntries={['/new/budget', '/new/confirm']} initialIndex={1}>
          <Routes>
            <Route path="/new/budget" element={<p>Set your budget</p>} />
            <Route path="/new/confirm" element={<Confirm />} />
            <Route path="/" element={<Dashboard />} />
          </Routes>
        </MemoryRouter>
      </NewStrategyDraftProvider>
    </AccountProvider>,
  )
}

describe('Confirm', () => {
  it('opens the dashboard for an AI strategy, and going back never reopens Confirm', async () => {
    open()
    const start = vi.spyOn(api, 'startStrategy').mockResolvedValue({ id: 'st' } as StrategySummary)

    fireEvent.click(await screen.findByRole('button', { name: 'Start the simulation' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Dashboard back' }))

    // Confirm would show the reset draft (Dip in winners), one tap from a second strategy.
    expect(await screen.findByText('Set your budget')).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Ready to start' })).toBeNull()
    expect(start).toHaveBeenCalledTimes(1)
    expect(start.mock.calls[0][0]).toMatchObject({ templateKey: 'ai' })
  })
})
