// @vitest-environment jsdom
import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type StrategySummary } from '../../api'
import { NewStrategyDraftProvider } from '../new-strategy/NewStrategyDraft'
import { Dashboard } from './Dashboard'

vi.mock('../account/AccountContext', () => ({
  useCurrentAccount: () => ({ email: 'reader@example.com', emailVerified: true }),
  useIsOwner: () => false,
}))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const summary = (fields: Partial<StrategySummary>): StrategySummary => ({
  id: 's',
  name: 'Strategy',
  templateKey: 'dip',
  templateName: 'Winners at a discount',
  mode: 'simulation',
  startedOn: '2026-10-01',
  currency: 'EUR',
  budget: 3000,
  used: 0,
  results: null,
  pastResult: { years: '2010-2024', template: 212, index: 180, worstYear: -21 },
  status: 'running',
  endedOn: null,
  archived: false,
  ...fields,
})

describe('Dashboard', () => {
  it('lists ended strategies with an ENDED tag after the running ones, and folds the archived ones away', async () => {
    vi.spyOn(api, 'getTemplates').mockResolvedValue([])
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-28',
      waiting: [],
      strategies: [
        summary({ id: 'r', name: 'Running one' }),
        summary({ id: 'e', name: 'Ended one', status: 'ended', endedOn: '2026-10-20' }),
        summary({ id: 'a', name: 'Archived one', status: 'ended', endedOn: '2026-10-10', archived: true }),
      ],
    })
    render(
      <NewStrategyDraftProvider owner="a@example.com">
        <MemoryRouter>
          <Dashboard />
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    const ended = await screen.findByRole('link', { name: /Ended one/ })
    expect(within(ended).getByText('ENDED')).toBeTruthy()
    expect(within(ended).getByText(/ended/)).toBeTruthy()
    expect(within(screen.getByRole('link', { name: /Running one/ })).queryByText('ENDED')).toBeNull()

    const archived = screen.getByText('Archived (1)').closest('details')!
    expect(archived.open).toBe(false)
    expect(within(archived).getByText('Archived one')).toBeTruthy()
  })
})
