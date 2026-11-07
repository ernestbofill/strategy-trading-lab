// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type TemplateDetail } from '../../../api'
import { invalidateAll } from '../../../lib/useLoadable'
import { NewStrategyDraftProvider } from '../NewStrategyDraft'
import { HowTested } from './HowTested'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const templateWithBacktest: TemplateDetail = {
  key: 'dip',
  engineKey: 'stage2_pullback',
  name: 'Winners at a discount',
  tagline: 'Buy the dip',
  idea: 'Buy when strong stocks drop temporarily',
  investsIn: 'stockList',
  rationale: 'Dip strategy',
  rules: [],
  signals: 'buysOnly',
  watches: { kind: 'stocks', starterList: [] },
  backtest: {
    scope: '1,146 stocks, 2000-2026.',
    years: '2000-2026',
    universe: '1,146 US stocks (S&P 500, S&P 400, Nasdaq-100, and dropouts)',
    testedDefaults: 'The tested defaults: €3,000 budget, €100 steps, up to 5 per stock.',
    provider: 'Yahoo Finance',
    feePercent: 0.2,
    feePerTrade: 1,
    periods: [
      { period: '2000-12', templatePerYear: 11.5, indexPerYear: 5.5, worstYear: -18, indexWorstYear: -37, alpha: 6.0 },
      { period: '2013-19', templatePerYear: 14.9, indexPerYear: 13.4, worstYear: -12, indexWorstYear: -4, alpha: 1.5 },
      { period: '2020-26', templatePerYear: 16.3, indexPerYear: 14.3, worstYear: -21, indexWorstYear: -18, alpha: 2.0 },
      { period: 'All', templatePerYear: 14.3, indexPerYear: 9.8, worstYear: -21, indexWorstYear: -37, alpha: 4.5 },
    ],
  },
  defaults: { version: 1, budget: { amount: 3000, step: 100, maxSteps: 5 }, params: {} },
  settings: [],
  settingGroups: [],
  aiRound: null,
}

const bearTemplate: TemplateDetail = {
  ...templateWithBacktest,
  key: 'bear',
  name: 'Bear markets',
  backtest: {
    ...templateWithBacktest.backtest!,
    periods: [
      { period: '2000-12', templatePerYear: 9.4, indexPerYear: 1.7, worstYear: -12, indexWorstYear: -37, alpha: 7.7, story: 'Bought 2001–02 & 2008–09 lows' },
      { period: '2013-19', templatePerYear: null, indexPerYear: null, worstYear: null, indexWorstYear: null, alpha: null, story: 'No market crashes' },
      { period: '2020-26', templatePerYear: 16.8, indexPerYear: 13.3, worstYear: -14, indexWorstYear: -18, alpha: 3.5, story: 'Bought 2020, 2022 & 2025 drops' },
      { period: 'All', templatePerYear: 11.2, indexPerYear: 7.7, worstYear: -14, indexWorstYear: -37, alpha: 3.5, story: '7 bear cycles across 3 crashes' },
    ],
  },
}

const trendTemplate: TemplateDetail = {
  ...templateWithBacktest,
  key: 'trend',
  name: 'Index trend',
  backtest: {
    ...templateWithBacktest.backtest!,
    periods: [
      { period: '2000-12', templatePerYear: 6.4, indexPerYear: 1.7, worstYear: -4, indexWorstYear: -37, alpha: 4.7, story: 'Dodged the 2000–02 and 2008 crashes' },
      { period: 'All', templatePerYear: 10.2, indexPerYear: 10.9, worstYear: -16, indexWorstYear: -37, alpha: -0.7, story: 'Nearly same return, less than half the crash' },
    ],
    benchmarks: [
      {
        key: 'sp500',
        name: 'S&P 500',
        periods: [
          { period: '2000-12', templatePerYear: 6.4, indexPerYear: 1.7, worstYear: -4, indexWorstYear: -37, alpha: 4.7, story: 'Dodged the 2000–02 and 2008 crashes' },
          { period: 'All', templatePerYear: 10.2, indexPerYear: 10.9, worstYear: -16, indexWorstYear: -37, alpha: -0.7, story: 'Nearly same return, less than half the crash' },
        ],
      },
      {
        key: 'qqq',
        name: 'Nasdaq-100',
        periods: [
          { period: '2000-12', templatePerYear: 4.8, indexPerYear: -1.2, worstYear: -15, indexWorstYear: -42, alpha: 6.0, story: 'Dodged the dot-com collapse and 2008' },
          { period: 'All', templatePerYear: 9.4, indexPerYear: 8.7, worstYear: -19, indexWorstYear: -42, alpha: 0.7, story: 'Beat buy-and-hold, cut worst fall from −83% to −42%' },
        ],
      },
    ],
  },
}

const templateWithoutBacktest: TemplateDetail = {
  ...templateWithBacktest,
  key: 'ai',
  name: "Your AI's top picks",
  backtest: null,
}

describe('HowTested', () => {
  it('renders backtest details, the By period table, the Alpha tooltip, and the yearly averages note', async () => {
    vi.spyOn(api, 'getTemplate').mockResolvedValue(templateWithBacktest)

    render(
      <NewStrategyDraftProvider owner="test@example.com">
        <MemoryRouter initialEntries={['/new/templates/dip/how-tested']}>
          <Routes>
            <Route path="/new/templates/:key/how-tested" element={<HowTested />} />
          </Routes>
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    expect(await screen.findByRole('heading', { name: 'How we tested' })).toBeTruthy()
    expect(screen.getByText('2000-2026')).toBeTruthy()
    expect(screen.getByText('1,146 US stocks (S&P 500, S&P 400, Nasdaq-100, and dropouts)')).toBeTruthy()

    // Period rows
    expect(screen.getByRole('rowheader', { name: '2000-12' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: '2013-19' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: '2020-26' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: 'All' })).toBeTruthy()

    // Alpha column and tooltip
    fireEvent.click(screen.getByRole('button', { name: 'What is alpha?' }))
    expect(
      screen.getByText('Direct alpha: the yearly advantage over putting the same money into the index on the same days.'),
    ).toBeTruthy()

    // Independent test periods note
    expect(
      screen.getByText(
        'All figures are yearly averages. Each period is an independent test that starts with fresh money.',
      ),
    ).toBeTruthy()

    // Updated survivor bias caveat
    expect(
      screen.getByText(
        "Historical tests use companies and indexes trading today. Failed or delisted companies from decades ago aren't included, which flatters past results.",
      ),
    ).toBeTruthy()
  })

  it('shows "No market crashes" for Bear markets in 2013-19 alongside the Story column', async () => {
    vi.spyOn(api, 'getTemplate').mockResolvedValue(bearTemplate)

    render(
      <NewStrategyDraftProvider owner="test@example.com">
        <MemoryRouter initialEntries={['/new/templates/bear/how-tested']}>
          <Routes>
            <Route path="/new/templates/:key/how-tested" element={<HowTested />} />
          </Routes>
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    expect(await screen.findByText('No market crashes')).toBeTruthy()
    expect(screen.getByText('Bought 2001–02 & 2008–09 lows')).toBeTruthy()
    expect(screen.getByText('7 bear cycles across 3 crashes')).toBeTruthy()
  })

  it('switches between index tabs on Index trend and updates the Story column', async () => {
    vi.spyOn(api, 'getTemplate').mockResolvedValue(trendTemplate)

    render(
      <NewStrategyDraftProvider owner="test@example.com">
        <MemoryRouter initialEntries={['/new/templates/trend/how-tested']}>
          <Routes>
            <Route path="/new/templates/:key/how-tested" element={<HowTested />} />
          </Routes>
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    const sp500Tab = await screen.findByRole('tab', { name: 'S&P 500' })
    expect(sp500Tab.getAttribute('aria-selected')).toBe('true')
    expect(screen.getByText('Dodged the 2000–02 and 2008 crashes')).toBeTruthy()

    const qqqTab = screen.getByRole('tab', { name: 'Nasdaq-100' })
    fireEvent.click(qqqTab)
    expect(qqqTab.getAttribute('aria-selected')).toBe('true')
    expect(screen.getByText('Dodged the dot-com collapse and 2008')).toBeTruthy()
    expect(screen.getByText('Beat buy-and-hold, cut worst fall from −83% to −42%')).toBeTruthy()
  })

  it('renders no backtest message when template has no backtest', async () => {
    vi.spyOn(api, 'getTemplate').mockResolvedValue(templateWithoutBacktest)

    render(
      <NewStrategyDraftProvider owner="test@example.com">
        <MemoryRouter initialEntries={['/new/templates/ai/how-tested']}>
          <Routes>
            <Route path="/new/templates/:key/how-tested" element={<HowTested />} />
          </Routes>
        </MemoryRouter>
      </NewStrategyDraftProvider>,
    )

    expect(await screen.findByText('There is no backtest for this template.')).toBeTruthy()
  })
})
