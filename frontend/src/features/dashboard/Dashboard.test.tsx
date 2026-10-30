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
  ...fields,
})

describe('Dashboard', () => {
  it('folds the archived strategies away after the running ones, with an ARCHIVED tag', async () => {
    vi.spyOn(api, 'getTemplates').mockResolvedValue([])
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-28',
      waiting: [],
      strategies: [
        summary({ id: 'r', name: 'Running one' }),
        summary({ id: 'e', name: 'Archived lately', status: 'ended', endedOn: '2026-10-20' }),
        summary({ id: 'a', name: 'Archived long ago', status: 'ended', endedOn: '2026-10-10' }),
      ],
    })
    render(
      <NewStrategyDraftProvider owner="a@example.com">
        <MemoryRouter>
          <Dashboard />
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    expect(within(await screen.findByRole('link', { name: /Running one/ })).queryByText('ARCHIVED')).toBeNull()
    const archived = screen.getByText('Archived (2)').closest('details')!
    expect(archived.open).toBe(false)
    const lately = within(archived).getByRole('link', { name: /Archived lately/ })
    expect(within(lately).getByText('ARCHIVED')).toBeTruthy()
    expect(within(lately).getByText(/archived Oct 20/)).toBeTruthy()
    expect(within(archived).getByText('Archived long ago')).toBeTruthy()
  })
})
