// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type DailySummary } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { SummaryPage } from './SummaryPage'

vi.mock('../account/AccountContext', () => ({
  useCurrentAccount: () => ({ email: 'trader@example.com', emailVerified: true, locale: 'en' }),
  useOptionalAccount: () => ({ email: 'trader@example.com', emailVerified: true, locale: 'en' }),
  useRole: () => 'user',
  useAccount: () => ({
    state: { status: 'ready', account: { email: 'trader@example.com', emailVerified: true, locale: 'en' } },
    refresh: vi.fn(),
    owner: 'trader@example.com',
    clear: vi.fn(),
  }),
  useIsOwner: () => false,
}))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const mockSummary = (fields: Partial<DailySummary>): DailySummary => ({
  date: '2026-10-23',
  hasChanges: false,
  waitingSignals: [],
  waitingAiRounds: [],
  whatChanged: [],
  stockChanges: [],
  strategies: [],
  ...fields,
})

describe('SummaryPage', () => {
  it('renders quiet state on days with no changes and shows running strategies scoreboard', async () => {
    vi.spyOn(api, 'getSummary').mockResolvedValue(
      mockSummary({
        hasChanges: false,
        strategies: [
          {
            strategyId: 'strat-1',
            strategyName: 'Alpha Dip',
            mode: 'live',
            startedOn: '2026-10-01',
            signalsCount: 3,
          },
        ],
      }),
    )

    render(
      <MemoryRouter>
        <SummaryPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('No changes today')).toBeTruthy()
    expect(
      screen.getByText('Your strategies keep running with no new signals or stock changes.'),
    ).toBeTruthy()

    expect(screen.queryByText('Waiting for you')).toBeNull()
    expect(screen.queryByText('What changed today')).toBeNull()
    expect(screen.queryByText('Stock changes')).toBeNull()

    const stratLink = screen.getByRole('link', { name: /Alpha Dip/i })
    expect(stratLink.getAttribute('href')).toBe('/strategies/strat-1')
    expect(screen.getByText('since Oct 1')).toBeTruthy()
    expect(screen.getByText(/3 signals/)).toBeTruthy()
  })

  it('renders what changed, waiting items, stock changes, and scoreboard on active days', async () => {
    vi.spyOn(api, 'getSummary').mockResolvedValue(
      mockSummary({
        hasChanges: true,
        waitingSignals: [
          {
            signalId: 'sig-1',
            strategyId: 'strat-1',
            strategyName: 'Alpha Dip',
            stockName: 'Apple',
            side: 'buy',
            signalDate: '2026-10-23',
            stepNo: 2,
            maxSteps: 5,
            closePrice: 150,
          },
        ],
        waitingAiRounds: [
          {
            strategyId: 'strat-ai',
            strategyName: 'AI Growth',
          },
        ],
        whatChanged: [
          {
            signalId: 'sig-1',
            strategyId: 'strat-1',
            strategyName: 'Alpha Dip',
            mode: 'live',
            stockName: 'Apple',
            side: 'buy',
            signalDate: '2026-10-23',
            closePrice: 150,
            stepNo: 2,
            maxSteps: 5,
            text: 'Apple: rules for Alpha Dip are met',
          },
        ],
        stockChanges: [
          {
            strategyId: 'strat-1',
            assetId: 'asset-1',
            strategyName: 'Alpha Dip',
            mode: 'live',
            stockName: 'Tesla',
            kind: 'cameClose',
            distancePct: 2.5,
            text: 'Tesla, Alpha Dip: now 2.5% above discount zone.',
          },
        ],
        strategies: [
          {
            strategyId: 'strat-1',
            strategyName: 'Alpha Dip',
            mode: 'live',
            startedOn: '2026-10-01',
            signalsCount: 1,
          },
        ],
      }),
    )

    render(
      <MemoryRouter>
        <SummaryPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Waiting for you')).toBeTruthy()
    expect(screen.queryByText('No changes today')).toBeNull()

    const signalLink = screen.getByRole('link', { name: /Apple: your rules for Alpha Dip are met/i })
    expect(signalLink.getAttribute('href')).toBe('/strategies/strat-1/signals/sig-1')
    expect(screen.getByText('Step 2 of 5')).toBeTruthy()

    const aiLink = screen.getByRole('link', { name: /Time for the AI round of AI Growth/i })
    expect(aiLink.getAttribute('href')).toBe('/strategies/strat-ai/ai-round')

    expect(screen.getByText('What changed today')).toBeTruthy()
    const changedLink = screen.getByRole('link', { name: /Apple: rules for Alpha Dip are met/i })
    expect(changedLink.getAttribute('href')).toBe('/strategies/strat-1/signals/sig-1')

    expect(screen.getByText('Stock changes')).toBeTruthy()
    const stockLink = screen.getByRole('link', { name: /Tesla, Alpha Dip: now 2.5% above discount zone/i })
    expect(stockLink.getAttribute('href')).toBe('/strategies/strat-1/stocks/asset-1')
  })

  it('renders helpful empty state when user has no running strategies', async () => {
    vi.spyOn(api, 'getSummary').mockResolvedValue(
      mockSummary({
        hasChanges: false,
        strategies: [],
      }),
    )

    render(
      <MemoryRouter>
        <SummaryPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('No running strategies yet.')).toBeTruthy()
    const startLink = screen.getByRole('link', { name: 'Pick a template to start' })
    expect(startLink.getAttribute('href')).toBe('/new')
  })

  it('provides DASHBOARD link in TopBar pointing to home', async () => {
    vi.spyOn(api, 'getSummary').mockResolvedValue(mockSummary({}))

    render(
      <MemoryRouter>
        <SummaryPage />
      </MemoryRouter>,
    )

    const homeLink = await screen.findByRole('link', { name: 'DASHBOARD' })
    expect(homeLink.getAttribute('href')).toBe('/')
  })
})
