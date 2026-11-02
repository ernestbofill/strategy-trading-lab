// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, InvalidFieldsError, type StockRow, type StrategyDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { EditList } from './EditList'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const row = (assetId: string, name: string, status: StockRow['status'] = 'watching'): StockRow => ({
  assetId, symbol: assetId.toUpperCase(), name, status, steps: 0, rulesSteps: null, maxSteps: 5, distancePct: null, signalId: null,
  signalStep: null, invested: 0, result: null, exchange: 'NASDAQ', currency: 'USD', what: `${name} does things`, checkedOn: null,
  close: null, checks: [],
})

const strategy = {
  id: 's1', name: 'Dips', templateKey: 'dip', templateName: 'Winners at a discount', investsIn: 'stockList', mode: 'live',
  status: 'running', endedOn: null, startedOn: '2026-10-13', today: '2026-10-24', currency: 'EUR',
  budget: { amount: 3000, used: 0, step: 100, maxSteps: 5, steps: 0, totalSteps: 30 }, results: null, pastResult: null,
  signals: null, words: null, trades: [], signalHistory: [], rates: [], settings: {},
  stocks: [row('a', 'Apple'), row('z', 'Zscaler'), row('o', 'Old Co', 'removed')],
} as StrategyDetail

function renderList(s: StrategyDetail = strategy) {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getStrategy').mockResolvedValue(s)
  render(
    <MemoryRouter initialEntries={['/strategies/s1/list']}>
      <Routes>
        <Route path="/strategies/:id/list" element={<EditList />} />
        <Route path="/strategies/:id" element={<p>Strategy page</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('EditList', () => {
  it('ticks the followed stocks, leaves a removed one unticked, and saves the ticked ones', async () => {
    const save = vi.spyOn(api, 'updateStrategyAssets').mockResolvedValue()
    renderList()

    const apple = await screen.findByRole('checkbox', { name: /Apple/ })
    expect((apple as HTMLInputElement).checked).toBe(true)
    expect((screen.getByRole('checkbox', { name: /Old Co/ }) as HTMLInputElement).checked).toBe(false)
    expect(screen.getByText('2 of 3 ticked')).toBeDefined()

    fireEvent.click(apple)
    fireEvent.click(screen.getByRole('checkbox', { name: /Old Co/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Save the list' }))

    await screen.findByText('Strategy page')
    expect(save).toHaveBeenCalledWith('s1', ['z', 'o'])
  })

  it('has nothing to edit on an archived strategy', async () => {
    renderList({ ...strategy, status: 'ended', endedOn: '2026-10-20' })
    expect(await screen.findByText('This strategy is archived, so its list can’t change.')).toBeDefined()
    expect(screen.queryByRole('checkbox')).toBeNull()
  })

  it("can't save an empty list, and shows the server's reason", async () => {
    vi.spyOn(api, 'updateStrategyAssets').mockRejectedValue(new InvalidFieldsError({ assetIds: 'Use at most 60 stocks.' }))
    renderList()

    fireEvent.click(await screen.findByRole('checkbox', { name: /Apple/ }))
    fireEvent.click(screen.getByRole('checkbox', { name: /Zscaler/ }))
    expect(screen.getByText('Save the list').getAttribute('aria-disabled')).toBe('true')
    expect(screen.getByText('Tick at least one stock to watch.')).toBeDefined()

    fireEvent.click(screen.getByRole('checkbox', { name: /Zscaler/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Save the list' }))
    await waitFor(() => expect(screen.getByRole('alert').textContent).toBe('Use at most 60 stocks.'))
  })
})
