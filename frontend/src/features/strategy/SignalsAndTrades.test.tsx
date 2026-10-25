// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  api,
  type StrategyDetail,
  type StrategySignalItem,
  type StrategyTradeItem,
} from '../../api'
import { SignalsAndTrades } from './SignalsAndTrades'

afterEach(cleanup)

const mockSignal = (fields: Partial<StrategySignalItem>): StrategySignalItem => ({
  id: fields.id ?? 'sig-1',
  assetId: fields.assetId ?? 'asset-1',
  symbol: fields.symbol ?? 'ZS',
  stockName: fields.stockName ?? 'Zscaler',
  signalDate: fields.signalDate ?? '2026-10-22',
  side: fields.side ?? 'buy',
  stepNo: fields.stepNo ?? 1,
  maxSteps: fields.maxSteps ?? 5,
  stepAmount: fields.stepAmount ?? 100,
  closePrice: fields.closePrice ?? 180,
  assetCurrency: fields.assetCurrency ?? 'USD',
  reason: fields.reason ?? '8% below its peak',
  status: fields.status ?? 'confirmed',
  skipReason: fields.skipReason ?? null,
  snoozedUntil: fields.snoozedUntil ?? null,
  endedOn: fields.endedOn ?? null,
  createdAt: fields.createdAt ?? '2026-10-22T20:00:00Z',
  ...fields,
})

const mockTrade = (fields: Partial<StrategyTradeItem>): StrategyTradeItem => ({
  id: fields.id ?? 'trade-1',
  assetId: fields.assetId ?? 'asset-1',
  symbol: fields.symbol ?? 'ZS',
  stockName: fields.stockName ?? 'Zscaler',
  tradeDate: fields.tradeDate ?? '2026-10-23',
  side: fields.side ?? 'buy',
  price: fields.price ?? 185,
  assetCurrency: fields.assetCurrency ?? 'USD',
  enteredAs: fields.enteredAs ?? 'amount',
  amount: fields.amount ?? 98.80,
  shares: fields.shares ?? 0.54,
  fee: fields.fee ?? 1.20,
  strategyCurrency: fields.strategyCurrency ?? 'EUR',
  fxRate: fields.fxRate ?? 1.08,
  fxSource: fields.fxSource ?? 'reference',
  source: fields.source ?? 'signal',
  signalId: fields.signalId ?? 'sig-1',
  ...fields,
})

const mockStrategy = (fields: Partial<StrategyDetail>): StrategyDetail => ({
  id: 'strat-1',
  name: 'Tech Dip',
  templateKey: 'dip',
  templateName: 'Dip in winners',
  mode: 'live',
  startedOn: '2026-10-01',
  today: '2026-10-25',
  currency: 'EUR',
  budget: { amount: 3000, used: 100, step: 100, maxSteps: 5, steps: 1, totalSteps: 30 },
  results: null,
  pastResult: null,
  signals: { did: 1, skipped: 0, missed: 0 },
  words: null,
  stocks: [
    {
      assetId: 'asset-1',
      symbol: 'ZS',
      name: 'Zscaler',
      status: 'building',
      steps: 1,
      rulesSteps: 1,
      maxSteps: 5,
      distancePct: null,
      signalId: null,
      signalStep: null,
      invested: 100,
      result: 2.5,
    },
  ],
  trades: [],
  signalHistory: [],
  rates: [{ currency: 'USD', rates: [{ date: '2026-10-23', rate: 1.08 }] }],
  ...fields,
})

describe('SignalsAndTrades', () => {
  it('renders empty message when there are no signals or trades', () => {
    render(
      <MemoryRouter>
        <SignalsAndTrades s={mockStrategy({ trades: [], signalHistory: [] })} />
      </MemoryRouter>,
    )

    expect(screen.getByText('Signals and trades')).toBeTruthy()
    expect(screen.getByText('No signals or trades yet.')).toBeTruthy()
  })

  it('orders signals and trades newest date first and displays statuses', () => {
    const s = mockStrategy({
      trades: [
        mockTrade({ id: 't1', tradeDate: '2026-10-23', shares: 0.54, price: 185, amount: 98.80 }),
        mockTrade({ id: 't2', tradeDate: '2026-10-24', source: 'manual', shares: 1, price: 190, amount: 175 }),
      ],
      signalHistory: [
        mockSignal({ id: 's1', signalDate: '2026-10-22', status: 'confirmed' }),
        mockSignal({ id: 's2', signalDate: '2026-10-25', status: 'waiting', snoozedUntil: '2026-10-26' }),
      ],
    })

    const { container } = render(
      <MemoryRouter>
        <SignalsAndTrades s={s} />
      </MemoryRouter>,
    )

    expect(screen.getByText('SNOOZED')).toBeTruthy()
    expect(screen.getByText('DID')).toBeTruthy()
    expect(screen.getByText('MANUAL')).toBeTruthy()

    // Match only the top rowDate elements
    const dateElements = container.querySelectorAll('[class*="rowDate"]')
    const dates = Array.from(dateElements).map((el) => el.textContent)
    expect(dates[0]).toBe('Oct 25')
    expect(dates[1]).toBe('Oct 24')
    expect(dates[2]).toBe('Oct 23')
    expect(dates[3]).toBe('Oct 22')
  })

  it('allows logging a manual trade in live mode', async () => {
    const logSpy = vi.spyOn(api, 'logManualTrade').mockResolvedValue(
      mockTrade({ id: 't-new', source: 'manual', price: 185, amount: 98.80 }),
    )

    const s = mockStrategy({ mode: 'live' })
    render(
      <MemoryRouter>
        <SignalsAndTrades s={s} />
      </MemoryRouter>,
    )

    const logBtn = screen.getByText('Log a trade by hand')
    fireEvent.click(logBtn)

    expect(screen.getByText('Log a trade by hand')).toBeTruthy()

    // Fill in price and amount
    const priceInput = screen.getByLabelText(/Price per share/i)
    fireEvent.change(priceInput, { target: { value: '185' } })

    const amountInput = screen.getByLabelText(/Amount/i)
    fireEvent.change(amountInput, { target: { value: '98.80' } })

    const saveBtn = screen.getByRole('button', { name: 'Save trade' })
    fireEvent.click(saveBtn)

    await waitFor(() => {
      expect(logSpy).toHaveBeenCalledWith(
        'strat-1',
        expect.objectContaining({
          assetId: 'asset-1',
          side: 'buy',
          price: 185,
          amount: 98.80,
        }),
      )
    })
  })

  it('allows editing a trade in live mode', async () => {
    const updateSpy = vi.spyOn(api, 'updateTrade').mockResolvedValue()
    const s = mockStrategy({
      mode: 'live',
      trades: [mockTrade({ id: 't1', price: 185, amount: 98.80, shares: 0.54 })],
    })

    render(
      <MemoryRouter>
        <SignalsAndTrades s={s} />
      </MemoryRouter>,
    )

    const editBtn = screen.getByText('Edit')
    fireEvent.click(editBtn)

    expect(screen.getByText(/Edit trade · Zscaler/)).toBeTruthy()

    const priceInput = screen.getByLabelText(/Price per share/i)
    fireEvent.change(priceInput, { target: { value: '188' } })

    const saveBtn = screen.getByRole('button', { name: 'Save changes' })
    fireEvent.click(saveBtn)

    await waitFor(() => {
      expect(updateSpy).toHaveBeenCalledWith(
        't1',
        expect.objectContaining({
          price: 188,
        }),
      )
    })
  })

  it('allows deleting a trade with confirmation in live mode', async () => {
    const deleteSpy = vi.spyOn(api, 'deleteTrade').mockResolvedValue()
    const s = mockStrategy({
      mode: 'live',
      trades: [mockTrade({ id: 't1' })],
    })

    render(
      <MemoryRouter>
        <SignalsAndTrades s={s} />
      </MemoryRouter>,
    )

    const deleteBtn = screen.getByText('Delete')
    fireEvent.click(deleteBtn)

    expect(screen.getByText('Delete this trade? This will remove it from your results.')).toBeTruthy()

    const confirmBtn = screen.getByRole('button', { name: 'Delete' })
    fireEvent.click(confirmBtn)

    await waitFor(() => {
      expect(deleteSpy).toHaveBeenCalledWith('t1')
    })
  })

  it('does not show edit, delete, or manual trade buttons in simulation mode', () => {
    const s = mockStrategy({
      mode: 'simulation',
      trades: [mockTrade({ id: 't1' })],
      signalHistory: [mockSignal({ id: 's1' })],
    })

    render(
      <MemoryRouter>
        <SignalsAndTrades s={s} />
      </MemoryRouter>,
    )

    expect(screen.queryByText('Log a trade by hand')).toBeNull()
    expect(screen.queryByText('Edit')).toBeNull()
    expect(screen.queryByText('Delete')).toBeNull()
  })
})
