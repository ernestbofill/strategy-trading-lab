// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
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
  engineKey: 'dip_in_winners',
  name: 'Winners at a discount',
  tagline: 'Buy the dip',
  idea: 'Buy when strong stocks drop temporarily',
  investsIn: 'stockList',
  rationale: 'Dip strategy',
  rules: [],
  signals: 'buysOnly',
  watches: { kind: 'stocks', starterList: [] },
  backtest: {
    scope: '1,146 stocks, 2000-2024.',
    years: '2000-2024',
    universe: '1,146 US stocks (S&P 500, S&P 400, Nasdaq-100, and dropouts)',
    testedDefaults: 'The tested defaults: €3,000 budget, €100 steps, up to 5 per stock.',
    provider: 'Yahoo Finance',
    feePercent: 0.2,
    feePerTrade: 1,
    periods: [
      { period: '2000-09', years: 10, template: 48, index: -8, worstYear: -18, indexWorstYear: -37, templatePerYear: 4, indexPerYear: 5 },
      { period: '2010-19', years: 10, template: 185, index: 246, worstYear: -12, indexWorstYear: -37, templatePerYear: 4, indexPerYear: 5 },
      { period: '2020-24', years: 10, template: 72, index: 96, worstYear: -21, indexWorstYear: -37, templatePerYear: 4, indexPerYear: 5 },
      { period: 'All', years: 10, template: 490, index: 538, worstYear: -21, indexWorstYear: -37, templatePerYear: 4, indexPerYear: 5 },
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
  it('renders backtest details, the By period table, and the independent periods note', async () => {
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
    expect(screen.getByText('2000-2024')).toBeTruthy()
    expect(screen.getByText('1,146 US stocks (S&P 500, S&P 400, Nasdaq-100, and dropouts)')).toBeTruthy()

    // Period rows
    expect(screen.getByRole('rowheader', { name: '2000-09' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: '2010-19' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: '2020-24' })).toBeTruthy()
    expect(screen.getByRole('rowheader', { name: 'All' })).toBeTruthy()

    // Independent test periods note
    expect(
      screen.getByText(
        "Each period is an independent test starting with fresh money at its beginning, which is why the periods don't add up to All.",
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
