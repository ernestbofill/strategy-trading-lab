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

  it('shows customized copy when strategy settings or stocks differ from defaults', async () => {
    vi.spyOn(api, 'getTemplates').mockResolvedValue([])
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-28',
      waiting: [],
      strategies: [
        summary({ id: 'custom', name: 'Customized strategy', pastResult: { years: '2000-2024', template: 490, index: 538, worstYear: -21, isCustomized: true } }),
        summary({ id: 'default', name: 'Default strategy', pastResult: { years: '2000-2024', template: 490, index: 538, worstYear: -21, isCustomized: false } }),
      ],
    })
    render(
      <NewStrategyDraftProvider owner="a@example.com">
        <MemoryRouter>
          <Dashboard />
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    const customCard = within(await screen.findByRole('link', { name: /Customized strategy/ }))
    expect(customCard.getByText(/With the tested settings and stocks, the template did/)).toBeTruthy()

    const defaultCard = within(await screen.findByRole('link', { name: /Default strategy/ }))
    expect(defaultCard.getByText(/In the past, 2000-2024, the template did/)).toBeTruthy()
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
