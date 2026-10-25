// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, InvalidFieldsError, type SignalDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { SignalPage } from './SignalPage'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const detail = (fields: Partial<SignalDetail> = {}): SignalDetail => ({
  id: 'sig',
  strategyId: 'st',
  strategyName: 'Zscaler & friends',
  templateName: 'Dip in winners',
  strategyCurrency: 'EUR',
  stockName: 'Zscaler',
  assetCurrency: 'USD',
  stepNo: 2,
  maxSteps: 5,
  stepAmount: 100,
  signalDate: '2026-10-22',
  closePrice: 187.5,
  reason: '8.2% below its recent peak, still above its 50-day average',
  status: 'waiting',
  snoozedUntil: null,
  endedOn: null,
  today: '2026-10-24',
  prefill: { date: '2026-10-23', price: 190, amount: 98.8 },
  rates: [{ date: '2026-10-23', rate: 1.09 }],
  answer: null,
  ...fields,
})

function open(signal: SignalDetail) {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getSignal').mockResolvedValue(signal)
  render(
    <MemoryRouter initialEntries={['/strategies/st/signals/sig']}>
      <Routes>
        <Route path="/strategies/:id/signals/:signalId" element={<SignalPage />} />
        <Route path="/strategies/:id" element={<p>Strategy page</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('SignalPage', () => {
  it('describes the signal and saves the trade as prefilled, then goes back to the strategy', async () => {
    open(detail())
    const confirm = vi.spyOn(api, 'confirmSignal').mockResolvedValue()

    expect(await screen.findByRole('heading', { name: 'Zscaler' })).toBeTruthy()
    expect(screen.getByText(/8.2% below its recent peak/).textContent).toContain("Under your settings that's step 2 of 5 (€100).")
    expect(screen.getByText(/At/).textContent).toContain('$1.0900 per €1')
    fireEvent.change(screen.getByLabelText('Fee (EUR, optional)'), { target: { value: '1.20' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save the trade' }))

    await screen.findByText('Strategy page')
    expect(confirm).toHaveBeenCalledWith('sig', {
      date: '2026-10-23', price: 190, enteredAs: 'amount', amount: 98.8, shares: null, fee: 1.2, fxSource: 'reference', fxRate: null,
    })
  })

  it('describes a sell signal and defaults to shares prefilled with held shares', async () => {
    open(
      detail({
        side: 'sell',
        stepNo: null,
        maxSteps: null,
        stepAmount: null,
        reason: 'the market closed 3.1% below its 200-day average',
        prefill: { date: '2026-10-23', price: 190, amount: null, shares: 5.25 },
      }),
    )
    const confirm = vi.spyOn(api, 'confirmSignal').mockResolvedValue()

    expect(await screen.findByText(/are met for a sale: the market closed 3.1% below its 200-day average/)).toBeTruthy()
    expect((screen.getByLabelText('Shares') as HTMLInputElement).value).toBe('5.25')
    fireEvent.click(screen.getByRole('button', { name: 'Save the trade' }))

    await screen.findByText('Strategy page')
    expect(confirm).toHaveBeenCalledWith('sig', {
      date: '2026-10-23', price: 190, enteredAs: 'shares', amount: null, shares: 5.25, fee: null, fxSource: 'reference', fxRate: null,
    })
  })

  it('shows what the server refused under its field', async () => {
    open(detail())
    vi.spyOn(api, 'confirmSignal').mockRejectedValue(new InvalidFieldsError({ price: 'Enter the price per share.' }))

    fireEvent.click(await screen.findByRole('button', { name: 'Save the trade' }))

    expect(await screen.findByText('Enter the price per share.')).toBeTruthy()
  })

  it('skips with a reason and says which step comes next', async () => {
    open(detail())
    const skip = vi.spyOn(api, 'skipSignal').mockResolvedValue()

    fireEvent.click(await screen.findByLabelText('× I skipped it'))
    expect(screen.getByText(/A skip uses no budget/).textContent).toContain('the next signal for Zscaler is step 3 of 5.')
    fireEvent.click(screen.getByRole('button', { name: 'No cash' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save the skip' }))

    await waitFor(() => expect(skip).toHaveBeenCalledWith('sig', 'noCash'))
  })

  it('snoozes until the day the server gives', async () => {
    open(detail())
    vi.spyOn(api, 'snoozeSignal').mockResolvedValue('2026-10-26')

    fireEvent.click(await screen.findByRole('button', { name: 'Remind me next trading day' }))

    expect(await screen.findByText("We'll remind you on Oct 26.")).toBeTruthy()
  })

  it('shows an answered signal without the form', async () => {
    open(detail({ status: 'skipped', answer: { status: 'skipped', skipReason: 'noCash', date: null, price: null, amount: null, shares: null, fee: null } }))

    expect(await screen.findByText('You skipped it: no cash.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Save the trade' })).toBeNull()
  })
})
