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
      { period: '2000-12', templatePerYear: 11.5, indexPerYear: 5.5, worstYear: -18, indexWorstYear: -37, alpha: 6.0, story: 'Dodged the 2000–02 and 2008 crashes' },
      { period: '2013-19', templatePerYear: 14.9, indexPerYear: 13.4, worstYear: -12, indexWorstYear: -4, alpha: 1.5, story: 'Trailed the calm bull run' },
      { period: '2020-26', templatePerYear: 16.3, indexPerYear: 14.3, worstYear: -21, indexWorstYear: -18, alpha: 2.0, story: 'Sideways 2022, rode 2023–25' },
      { period: 'All', templatePerYear: 14.3, indexPerYear: 9.8, worstYear: -21, indexWorstYear: -37, alpha: 4.5, story: 'Same return, half the worst fall' },
    ],
    benchmarks: [
      {
        key: 'sp500',
        name: 'S&P 500',
        periods: [
          { period: '2000-12', templatePerYear: 11.5, indexPerYear: 5.5, worstYear: -18, indexWorstYear: -37, alpha: 6.0, story: 'Dodged the 2000–02 and 2008 crashes' },
          { period: '2013-19', templatePerYear: null, indexPerYear: null, worstYear: null, indexWorstYear: null, alpha: null, story: 'No market crashes' },
          { period: '2020-26', templatePerYear: 16.3, indexPerYear: 14.3, worstYear: -21, indexWorstYear: -18, alpha: 2.0, story: 'Sideways 2022, rode 2023–25' },
          { period: 'All', templatePerYear: 14.3, indexPerYear: 9.8, worstYear: -21, indexWorstYear: -37, alpha: 4.5, story: 'Same return, half the worst fall' },
        ],
      },
      {
        key: 'qqq',
        name: 'Nasdaq-100',
        periods: [
          { period: '2000-12', templatePerYear: 5.3, indexPerYear: -1.1, worstYear: -15, indexWorstYear: -42, alpha: 6.4, story: 'Dodged the dot-com collapse and 2008' },
          { period: 'All', templatePerYear: 9.0, indexPerYear: 8.6, worstYear: -22, indexWorstYear: -42, alpha: 0.4, story: 'Beat buy-and-hold, cut worst fall' },
        ],
      },
    ],
  },
  defaults: { version: 1, budget: { amount: 3000, step: 100, maxSteps: 5 }, params: {} },
  settings: [],
  settingGroups: [],
  aiRound: null,
}

const templateWithoutBacktest: TemplateDetail = {
  ...templateWithBacktest,
  key: 'ai',
  name: "Your AI's top picks",
  backtest: null,
}

describe('HowTested', () => {
  it('renders backtest details, the By period table, and the yearly averages note', async () => {
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

    // Period rows, null-period story, and Story column
    expect(screen.getByRole('rowheader', { name: '2000-12' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: '2013-19' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: '2020-26' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: 'All' })).toBeTruthy()
    expect(screen.getByText('No market crashes')).toBeTruthy()
    expect(screen.getByText('Dodged the 2000–02 and 2008 crashes')).toBeTruthy()

    // Alpha column tooltip and index tabs
    fireEvent.click(screen.getByRole('button', { name: 'What is alpha?' }))
    expect(
      screen.getByText('Direct alpha: the yearly advantage over putting the same money into the index on the same days.'),
    ).toBeTruthy()

    fireEvent.click(screen.getByRole('tab', { name: 'Nasdaq-100' }))
    expect(screen.getByText('Dodged the dot-com collapse and 2008')).toBeTruthy()

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
