// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, NameTakenError, type Dashboard, type StrategyDetail, type TemplateDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { NewStrategyDraftProvider, useNewStrategyDraft } from '../new-strategy/NewStrategyDraft'
import { StrategyPage } from './StrategyPage'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const detail = (fields: Partial<StrategyDetail>): StrategyDetail => ({
  id: fields.id ?? 's1',
  name: 'Zscaler & friends',
  templateKey: 'dip',
  templateName: 'Winners at a discount',
  mode: 'live',
  status: 'running',
  endedOn: null,
  archived: false,
  notifications: true,
  startedOn: '2026-10-13',
  today: '2026-10-24',
  currency: 'EUR',
  budget: { amount: 3000, used: 1200, step: 100, maxSteps: 5, steps: 12, totalSteps: 30 },
  results: null,
  pastResult: { years: '2010-2024', template: 212, index: 180, worstYear: -21 },
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
  it('shows a live strategy next to the rules alone and the index, and leads a waiting signal to the user', async () => {
    open(
      detail({
        id: 'live',
        results: { you: 4.2, rules: 5.1, index: -1.3, history: [] },
        stocks: [
          {
            assetId: 'a', symbol: 'ZS', name: 'Zscaler', status: 'waitingForYou', steps: 1, rulesSteps: 2, maxSteps: 5,
            distancePct: null, signalId: 'sig', signalStep: 2, invested: 101, result: 3.5,
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
    expect(screen.getByText(/The template's worst year, 2010-2024/).textContent).toContain('−21%')
    expect(screen.getByRole('link', { name: /Zscaler WAITING FOR YOU Signal: step 2 of 5/ }).getAttribute('href')).toBe(
      '/strategies/live/signals/sig',
    )
    expect(screen.getByRole('link', { name: /Tune the settings Change its rules/i }).getAttribute('href')).toBe(
      '/strategies/live/settings',
    )
    expect(screen.getByText(/Your results come from the trades you confirmed/).textContent).toContain(
      "Past results don't promise future ones.",
    )
  })

  it("shows a simulation without trades the template's past, with its worst year", async () => {
    open(detail({ id: 'sim', mode: 'simulation', signals: null, budget: { amount: 3000, used: 0, step: 100, maxSteps: 5, steps: 0, totalSteps: 30 } }))

    expect(await screen.findByText(/No paper trades yet/)).toBeTruthy()
    expect(screen.getByText('−21%')).toBeTruthy()
    expect(screen.queryByText('Signals')).toBeNull()
    expect(screen.getByText(/Results come from paper trades/)).toBeTruthy()
  })

  it('deletes the strategy after a confirm step and opens the dashboard', async () => {
    open(detail({ id: 'gone' }))
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
    open(detail({ id: 'kept' }))
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

  it("switches a live strategy's alerts", async () => {
    open(detail({ id: 'a' }))
    const alerts = vi.spyOn(api, 'setStrategyNotifications').mockResolvedValue()

    const box = await screen.findByRole('checkbox', { name: "Email me this strategy's signals" })
    expect((box as HTMLInputElement).checked).toBe(true)
    fireEvent.click(box)
    expect(alerts).toHaveBeenCalledWith('a', false)
  })

  it('has no alerts switch on a simulation that sends no emails', async () => {
    open(detail({ id: 'n', mode: 'simulation', signals: null }))

    expect(await screen.findByRole('button', { name: 'End the strategy' })).toBeTruthy()
    expect(screen.queryByRole('checkbox')).toBeNull()
  })

  it('ends the strategy after a confirm step', async () => {
    open(detail({ id: 'e' }))
    const end = vi.spyOn(api, 'endStrategy').mockResolvedValue()

    fireEvent.click(await screen.findByRole('button', { name: 'End the strategy' }))
    expect(screen.getByText(/End Zscaler & friends\? The daily check stops/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Keep it running' }))
    expect(end).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'End the strategy' }))
    fireEvent.click(screen.getByRole('button', { name: 'End it' }))
    expect(end).toHaveBeenCalledWith('e')
  })

  it('shows an ended strategy read-only, with Archive and Delete', async () => {
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
    const archive = vi.spyOn(api, 'archiveStrategy').mockResolvedValue()

    expect(await screen.findByText(/Ended on/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'End the strategy' })).toBeNull()
    expect(screen.queryByRole('checkbox')).toBeNull()
    expect(screen.queryByRole('link', { name: /Tune the settings/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /Log a trade by hand/ })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Delete the strategy' })).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Archive' }))
    expect(archive).toHaveBeenCalledWith('x', true)
  })

  it('offers to unarchive an archived strategy', async () => {
    open(detail({ id: 'u', status: 'ended', endedOn: '2026-10-20', archived: true }))
    const archive = vi.spyOn(api, 'archiveStrategy').mockResolvedValue()

    fireEvent.click(await screen.findByRole('button', { name: 'Unarchive' }))
    expect(archive).toHaveBeenCalledWith('u', false)
  })

  it('offers "Go live" on a running simulation only', async () => {
    open(detail({ id: 'gl-live' }))
    expect(await screen.findByRole('button', { name: 'End the strategy' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Go live/ })).toBeNull()
    cleanup()

    open(detail({ id: 'gl-ended', mode: 'simulation', signals: null, status: 'ended', endedOn: '2026-10-20' }))
    expect(await screen.findByRole('button', { name: 'Archive' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Go live/ })).toBeNull()
  })

  it('"Go live" fills the draft from the simulation and opens Choose the mode with Live picked', async () => {
    invalidateAll()
    vi.spyOn(api, 'getTemplate').mockResolvedValue(dipTemplate)
    vi.spyOn(api, 'getDashboard').mockResolvedValue({
      today: '2026-10-24',
      strategies: [{ name: 'Zscaler & friends live', status: 'running' }, { name: 'Old', status: 'ended' }],
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
          },
          {
            assetId: 'n', symbol: 'NET', name: 'Cloudflare', status: 'watching', steps: 0, rulesSteps: null, maxSteps: 4,
            distancePct: null, signalId: null, signalStep: null, invested: 0, result: null,
          },
        ],
      }),
    )

    const goLive = await screen.findByRole('button', { name: /Go live With these settings/ })
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
      name: 'Zscaler & friends live 2',
      currency: 'USD',
      sourceStrategyId: 'gl-sim',
    })
  })

  it('links a live copy to the simulation it came from', async () => {
    open(detail({ id: 'copy', sourceStrategyId: 'sim-9', sourceStrategyName: 'Zscaler sim' }))

    expect((await screen.findByRole('link', { name: 'Zscaler sim' })).getAttribute('href')).toBe('/strategies/sim-9')
    expect(screen.getByText(/Copied from/)).toBeTruthy()
  })
})
