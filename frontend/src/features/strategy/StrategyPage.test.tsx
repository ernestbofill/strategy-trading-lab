// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, NameTakenError, type Dashboard, type StrategyDetail, type StrategySummary, type TemplateDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { NewStrategyDraftProvider, useNewStrategyDraft } from '../new-strategy/NewStrategyDraft'
import { StrategyPage } from './StrategyPage'

// No push on this device: the push switch stays hidden (it has its own tests through Confirm and lib/push).
vi.mock('../account/usePushOffered', () => ({ usePushOffered: () => false }))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const detail = (fields: Partial<StrategyDetail>): StrategyDetail => ({
  id: fields.id ?? 's1',
  name: 'Zscaler & friends',
  templateKey: 'dip',
  templateName: 'Winners at a discount',
  investsIn: 'stockList',
  mode: 'live',
  status: 'running',
  endedOn: null,
  startedOn: '2026-10-13',
  today: '2026-10-24',
  currency: 'EUR',
  budget: { amount: 3000, used: 1200, step: 100, maxSteps: 5, steps: 12, totalSteps: 30 },
  results: null,
  pastResult: { years: '2010-2024', templatePerYear: 8.1, indexPerYear: 7.3, worstYear: -21, indexWorstYear: -18 },
  signals: { did: 9, skipped: 2, missed: 1 },
  words: { zone: 'the discount zone', notCandidate: 'Below its long trend' },
  stocks: [],
  trades: [],
  signalHistory: [],
  rates: [],
  ...fields,
})

function open(strategy: StrategyDetail) {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getStrategy').mockResolvedValue(strategy)
  vi.spyOn(api, 'getStockChart').mockResolvedValue({ startedOn: strategy.startedOn, days: [] })
  render(
    <NewStrategyDraftProvider owner="a@example.com">
      <MemoryRouter initialEntries={[`/strategies/${strategy.id}`]}>
        <Routes>
          <Route path="/strategies/:id" element={<StrategyPage />} />
          <Route path="/" element={<p>Dashboard</p>} />
          <Route path="/new/mode" element={<DraftProbe />} />
        </Routes>
      </MemoryRouter>
    </NewStrategyDraftProvider>,
  )
}

/** Stands in for Choose the mode: shows the draft it opens with. */
function DraftProbe() {
  const { load: _load, update: _update, switchTemplate: _switch, reset: _reset, ...draft } = useNewStrategyDraft()
  return <pre data-testid="draft">{JSON.stringify(draft)}</pre>
}

const dipTemplate = {
  key: 'dip',
  name: 'Winners at a discount',
  watches: {
    kind: 'stocks',
    starterList: [
      { id: 'a', symbol: 'ZS', name: 'Zscaler', what: '' },
      { id: 'b', symbol: 'CRWD', name: 'CrowdStrike', what: '' },
    ],
  },
  defaults: { version: 1, budget: { amount: 3000, step: 100, maxSteps: 5 }, params: { peakDays: 60 } },
  settings: [],
  settingGroups: [],
} as unknown as TemplateDetail

describe('StrategyPage', () => {
  it('shows a live strategy next to the rules alone and the index, and opens each stock page', async () => {
    open(
      detail({
        id: 'live',
        results: { you: 4.2, rules: 5.1, index: -1.3, history: [] },
        stocks: [
          {
            assetId: 'a', symbol: 'ZS', name: 'Zscaler', status: 'waitingForYou', steps: 1, rulesSteps: 2, maxSteps: 5,
            distancePct: null, signalId: 'sig', signalStep: 2, invested: 101, result: 3.5,
            exchange: 'NASDAQ', currency: 'USD', what: null, checkedOn: null, close: null, checks: [],
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
    expect(screen.getByText(/Worst year, 2010-2024/).textContent).toContain('the template −21%, an S&P 500 fund −18%')
    expect(screen.getByRole('link', { name: /Zscaler WAITING FOR YOU Signal: step 2 of 5/ }).getAttribute('href')).toBe(
      '/strategies/live/stocks/a',
    )
    expect(screen.getByRole('link', { name: /Tune the settings Change its rules/i }).getAttribute('href')).toBe(
      '/strategies/live/settings',
    )
    expect(screen.getByRole('heading', { name: 'Stocks · 1' })).toBeTruthy()
    expect(screen.queryByText(/Your results come from the trades you confirmed/)).toBeNull()
  })

  it('shows the first 5 stocks of a long list, and the rest on request', async () => {
    const stock = (n: number) => ({
      assetId: `s${n}`, symbol: `S${n}`, name: `Stock ${n}`, status: 'notCandidate' as const, steps: 0, rulesSteps: 0, maxSteps: 5,
      distancePct: null, signalId: null, signalStep: null, invested: 0, result: null,
      exchange: 'NASDAQ', currency: 'USD', what: null, checkedOn: '2026-10-22', close: 10, checks: [],
    })
    open(detail({ stocks: Array.from({ length: 8 }, (_, n) => stock(n + 1)) }))

    expect(await screen.findByRole('heading', { name: 'Stocks · 8' })).toBeTruthy()
    expect(screen.queryByText('Stock 6')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Show all 8' }))
    expect(screen.getByText('Stock 8')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Show fewer' }))
    expect(screen.queryByText('Stock 6')).toBeNull()
  })

  it("inlines a fund strategy's fund header, status and rules on the strategy page without a Funds list, and explains the S&P 500 fund when asked", async () => {
    open(
      detail({
        id: 'trend',
        name: 'S&P 500 index',
        templateKey: 'trend',
        investsIn: 'indexFund',
        mode: 'live',
        results: { you: 2, rules: 2, index: 3, history: [] },
        checkTexts: {
          buyLine: {
            label: 'Price vs the buy line',
            detail: '{+value}% vs the buy line ({level}, +3% above the SMA {midLevel}).',
            short: '{+value}% vs the buy line',
          },
        },
        stocks: [
          {
            assetId: 'cspx', symbol: 'CSPX.L', name: 'iShares Core S&P 500 UCITS ETF', status: 'waitingForYou', steps: 0, rulesSteps: 1, maxSteps: null,
            distancePct: 1.3, signalId: 'sig-1', signalStep: null, invested: 100, result: 2,
            exchange: 'XLON', currency: 'USD', what: 'Tracks the 500 largest US companies', checkedOn: '2026-11-06', close: 802.21,
            priceUpdatedAt: '2026-11-06T21:35:00Z', priceIsClose: true,
            checks: [{ key: 'buyLine', group: 'buy', met: false, value: -1.3, min: null, max: null, level: 812.91, midLevel: 789.23, endLevel: 765.55, progress: null, used: null }],
          },
        ],
      }),
    )

    expect(await screen.findByRole('heading', { name: 'S&P 500 index' })).toBeTruthy()
    expect(screen.getByText('CSPX.L')).toBeTruthy()
    expect(screen.getByText(/iShares Core S&P 500 UCITS ETF/)).toBeTruthy()
    expect(screen.getByText(/XLON/)).toBeTruthy()
    expect(screen.getByText('Tracks the 500 largest US companies')).toBeTruthy()
    expect(screen.getByText('$802.21')).toBeTruthy()
    expect(screen.getByText(/^Close on Nov 6, \d{2}:\d{2}$/)).toBeTruthy()
    expect(screen.getByText('WAITING FOR YOU')).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Rules on Nov 6' })).toBeTruthy()
    expect(screen.getByText('Price vs the buy line')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Answer the signal' }).getAttribute('href')).toBe('/strategies/trend/signals/sig-1')
    // No separate Funds list link or duplicate Position section on a single-fund strategy.
    expect(screen.queryByRole('heading', { name: /Funds/ })).toBeNull()
    expect(screen.queryByRole('heading', { name: /Your position|Trades/ })).toBeNull()

    const tip = screen.getByRole('button', { name: 'What is the S&P 500 fund?' })
    expect(screen.queryByText(/Buy and hold/)).toBeNull()
    fireEvent.click(tip)
    expect(tip.getAttribute('aria-expanded')).toBe('true')
    expect(screen.getByText(/Buy and hold/)).toBeTruthy()
    fireEvent.keyDown(document, { key: 'Escape' })
    expect(screen.queryByText(/Buy and hold/)).toBeNull()
  })

  it("shows a simulation without trades the template's past, with its worst year", async () => {
    open(detail({ id: 'sim', mode: 'simulation', signals: null, budget: { amount: 3000, used: 0, step: 100, maxSteps: 5, steps: 0, totalSteps: 30 } }))

    expect(await screen.findByText(/No trades yet/)).toBeTruthy()
    expect(screen.getByText('+8.1%')).toBeTruthy()
    expect(screen.getByText('−18%')).toBeTruthy()
    expect(screen.queryByText('Signals')).toBeNull()
    expect(screen.queryByText(/Results come from trades/)).toBeNull()
  })

  it('deletes an archived strategy after a confirm step and opens the dashboard', async () => {
    open(detail({ id: 'gone', status: 'ended', endedOn: '2026-10-20' }))
    const remove = vi.spyOn(api, 'deleteStrategy').mockResolvedValue()

    fireEvent.click(await screen.findByRole('button', { name: 'Delete the strategy' }))
    expect(screen.getByText(/Delete Zscaler & friends\? Its signals, trades and results go with it/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Keep it' }))
    expect(remove).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Delete the strategy' }))
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))
    expect(await screen.findByText('Dashboard')).toBeTruthy()
    expect(remove).toHaveBeenCalledWith('gone')
  })

  it('stays on the page and says so when the delete fails', async () => {
    open(detail({ id: 'kept', status: 'ended', endedOn: '2026-10-20' }))
    vi.spyOn(api, 'deleteStrategy').mockRejectedValue(new Error('HTTP 500'))

    fireEvent.click(await screen.findByRole('button', { name: 'Delete the strategy' }))
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))
    expect((await screen.findByRole('alert')).textContent).toBe("Couldn't delete it. Try again.")
    expect(screen.getByRole('heading', { name: 'Zscaler & friends' })).toBeTruthy()
  })

  it('renames the strategy in place and says when the name is taken', async () => {
    open(detail({ id: 'r' }))
    const rename = vi.spyOn(api, 'renameStrategy').mockRejectedValueOnce(new NameTakenError()).mockResolvedValueOnce()

    fireEvent.click(await screen.findByRole('button', { name: 'Rename' }))
    const field = screen.getByRole('textbox', { name: 'Name' })
    expect((field as HTMLInputElement).value).toBe('Zscaler & friends')
    expect(screen.queryByRole('button', { name: 'Rename' })).toBeNull()

    fireEvent.change(field, { target: { value: '  Taken  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect((await screen.findByRole('alert')).textContent).toBe('You already run a strategy with this name. Pick another.')
    expect(rename).toHaveBeenLastCalledWith('r', 'Taken')

    fireEvent.change(field, { target: { value: 'Free' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByRole('button', { name: 'Rename' })).toBeTruthy()
    expect(rename).toHaveBeenLastCalledWith('r', 'Free')
  })

  it('cancels a rename without saving', async () => {
    open(detail({ id: 'c' }))
    const rename = vi.spyOn(api, 'renameStrategy').mockResolvedValue()

    fireEvent.click(await screen.findByRole('button', { name: 'Rename' }))
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.getByRole('heading', { name: 'Zscaler & friends' })).toBeTruthy()
    expect(rename).not.toHaveBeenCalled()
  })

  it('archives a running strategy after a confirm step, and offers no delete while it runs', async () => {
    open(detail({ id: 'e' }))
    const archive = vi.spyOn(api, 'archiveStrategy').mockResolvedValue()

    fireEvent.click(await screen.findByRole('button', { name: 'Archive the strategy' }))
    expect(screen.queryByRole('button', { name: 'Delete the strategy' })).toBeNull()
    expect(screen.getByText(/Archive Zscaler & friends\? The daily check stops/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Keep it running' }))
    expect(archive).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Archive the strategy' }))
    fireEvent.click(screen.getByRole('button', { name: 'Archive it' }))
    expect(archive).toHaveBeenCalledWith('e')
  })

  it('shows an archived strategy read-only, with Delete only', async () => {
    open(
      detail({
        id: 'x',
        status: 'ended',
        endedOn: '2026-10-20',
        trades: [
          {
            id: 't1', assetId: 'a', symbol: 'ZS', stockName: 'Zscaler', tradeDate: '2026-10-14', side: 'buy', price: 100,
            assetCurrency: 'EUR', enteredAs: 'amount', amount: 99, shares: 0.99, fee: 1, strategyCurrency: 'EUR', fxRate: 1,
            fxSource: 'reference', source: 'manual', signalId: null,
          },
        ],
      }),
    )
    expect(await screen.findByText(/Archived on/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Archive the strategy' })).toBeNull()
    expect(screen.queryByRole('checkbox')).toBeNull()
    expect(screen.queryByRole('link', { name: /Tune the settings/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /Log a trade by hand/ })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Delete the strategy' })).toBeTruthy()
  })

  it('offers "Follow it manually" on a running simulation only', async () => {
    open(detail({ id: 'gl-live' }))
    expect(await screen.findByRole('button', { name: 'Archive the strategy' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Follow it manually/ })).toBeNull()
    cleanup()

    open(detail({ id: 'gl-ended', mode: 'simulation', signals: null, status: 'ended', endedOn: '2026-10-20' }))
    expect(await screen.findByRole('button', { name: 'Delete the strategy' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Follow it manually/ })).toBeNull()
  })

  it('"Follow it manually" fills the draft from the simulation and opens Choose the mode with Manual picked', async () => {
    invalidateAll()
    vi.spyOn(api, 'getTemplate').mockResolvedValue(dipTemplate)
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-24',
      strategies: [{ name: 'Zscaler & friends (manual)', status: 'running' }, { name: 'Old', status: 'ended' }],
      waiting: [],
    } as unknown as Dashboard)
    open(
      detail({
        id: 'gl-sim',
        mode: 'simulation',
        signals: null,
        currency: 'USD',
        budget: { amount: 6000, used: 0, step: 200, maxSteps: 4, steps: 0, totalSteps: 30 },
        settings: { peakDays: 40 },
        stocks: [
          {
            assetId: 'a', symbol: 'ZS', name: 'Zscaler', status: 'watching', steps: 0, rulesSteps: null, maxSteps: 4,
            distancePct: null, signalId: null, signalStep: null, invested: 0, result: null,
            exchange: 'NASDAQ', currency: 'USD', what: null, checkedOn: null, close: null, checks: [],
          },
          {
            assetId: 'n', symbol: 'NET', name: 'Cloudflare', status: 'watching', steps: 0, rulesSteps: null, maxSteps: 4,
            distancePct: null, signalId: null, signalStep: null, invested: 0, result: null,
            exchange: 'NASDAQ', currency: 'USD', what: null, checkedOn: null, close: null, checks: [],
          },
        ],
      }),
    )

    const goLive = await screen.findByRole('button', { name: /Follow it manually With these settings/ })
    await vi.waitFor(() => expect((goLive as HTMLButtonElement).disabled).toBe(false))
    // The dashboard's names number the copy.
    await vi.waitFor(() => expect(api.getDashboard).toHaveBeenCalled())
    await act(async () => {
      await vi.mocked(api.getDashboard).mock.results[0].value
    })
    fireEvent.click(goLive)

    const draft = JSON.parse((await screen.findByTestId('draft')).textContent!)
    expect(draft).toMatchObject({
      templateKey: 'dip',
      mode: 'live',
      unticked: ['CRWD'],
      added: [{ id: 'n', symbol: 'NET', name: 'Cloudflare', pricesIn: true }],
      settings: { peakDays: 40 },
      budget: 6000,
      step: 200,
      maxSteps: 4,
      name: 'Zscaler & friends (manual) 2',
      currency: 'USD',
      sourceStrategyId: 'gl-sim',
    })
  })

  it('links a live copy to the simulation it came from', async () => {
    open(detail({ id: 'copy', sourceStrategyId: 'sim-9', sourceStrategyName: 'Zscaler sim' }))

    expect((await screen.findByRole('link', { name: 'Zscaler sim' })).getAttribute('href')).toBe('/strategies/sim-9')
    expect(screen.getByText(/Copied from/)).toBeTruthy()
  })

  it('disables "Follow it manually" when running strategies is at 10', async () => {
    vi.spyOn(api, 'getTemplate').mockResolvedValue(dipTemplate)
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-28',
      waiting: [],
      strategies: Array.from({ length: 10 }, (_, i) => ({ id: `s${i}`, name: `S${i}`, status: 'running' as const } as unknown as StrategySummary)),
    })
    open(detail({ id: 'gl-sim', mode: 'simulation' }))
    const goLive = await screen.findByRole('button', { name: /Follow it manually You run 10 strategies/ })
    expect((goLive as HTMLButtonElement).disabled).toBe(true)
  })
})
