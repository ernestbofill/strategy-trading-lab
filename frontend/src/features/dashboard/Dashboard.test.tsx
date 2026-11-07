// @vitest-environment jsdom
import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type StrategySummary } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { NewStrategyDraftProvider } from '../new-strategy/NewStrategyDraft'
import { Dashboard } from './Dashboard'

vi.mock('../account/AccountContext', () => ({
  useCurrentAccount: () => ({ email: 'reader@example.com', emailVerified: true }),
  useOptionalAccount: () => ({ email: 'reader@example.com', emailVerified: true }),
  useRole: () => 'user',
  useAccount: () => ({
    state: { status: 'ready', account: { email: 'reader@example.com', emailVerified: true } },
    refresh: vi.fn(),
    owner: 'reader@example.com',
    clear: vi.fn(),
  }),
  useIsOwner: () => false,
}))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
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
  pastResult: { years: '2010-2024', templatePerYear: 8.1, indexPerYear: 7.3, worstYear: -21, indexWorstYear: -18 },
  status: 'running',
  endedOn: null,
  indexSinceStart: 1.2,
  stocks: { signals: 0, close: 1, candidates: 6, holding: 0, notCandidates: 3 },
  fund: null,
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

  it('shows the gain since the start next to the S&P 500, and where the stocks stand', async () => {
    vi.spyOn(api, 'getTemplates').mockResolvedValue([])
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-28',
      waiting: [],
      strategies: [
        summary({ id: 'list', name: 'Stock list' }),
        summary({
          id: 'fund',
          name: 'Fund one',
          fund: 'CSPX',
          results: { strategy: -2.5, index: -1.1, history: [] },
          stocks: { signals: 0, close: 0, candidates: 0, holding: 1, notCandidates: 0 },
        }),
      ],
    })
    render(
      <NewStrategyDraftProvider owner="a@example.com">
        <MemoryRouter>
          <Dashboard />
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    const list = within(await screen.findByRole('link', { name: /Stock list/ }))
    expect(list.getByText('0.0%')).toBeTruthy()
    expect(list.getByText('S&P 500: +1.2%')).toBeTruthy()
    expect(list.getByText('1 getting close')).toBeTruthy()
    expect(list.getByText('6 candidates')).toBeTruthy()
    expect(list.getByText('3 not a candidate')).toBeTruthy()

    const fund = within(screen.getByRole('link', { name: /Fund one/ }))
    expect(fund.getByText('−2.5%')).toBeTruthy()
    expect(fund.getByText('S&P 500: −1.1%')).toBeTruthy()
    expect(fund.getByText('CSPX: holding')).toBeTruthy()
  })

  it('disables the new strategy button at the limit of 10 running strategies', async () => {
    vi.spyOn(api, 'getTemplates').mockResolvedValue([])
    const running = Array.from({ length: 10 }, (_, i) => summary({ id: `s${i}`, name: `Running ${i}` }))
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-28',
      waiting: [],
      strategies: running,
    })
    render(
      <NewStrategyDraftProvider owner="a@example.com">
        <MemoryRouter>
          <Dashboard />
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    const btn = await screen.findByRole('button', { name: /You run 10 strategies, the most for now/ })
    expect((btn as HTMLButtonElement).disabled).toBe(true)
    expect(screen.queryByRole('link', { name: '+ New strategy' })).toBeNull()
  })

  it('renders the new strategy card placeholder in the running cards grid', async () => {
    vi.spyOn(api, 'getTemplates').mockResolvedValue([])
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-28',
      waiting: [],
      strategies: [summary({ id: 'r', name: 'Running one' })],
    })
    render(
      <NewStrategyDraftProvider owner="a@example.com">
        <MemoryRouter>
          <Dashboard />
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    const newStrategyLink = await screen.findByRole('link', { name: /\+ New strategy/i })
    expect(newStrategyLink).toBeTruthy()
    expect(newStrategyLink.getAttribute('href')).toBe('/new')
  })
})
