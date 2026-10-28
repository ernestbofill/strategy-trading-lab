// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, ForbiddenError, type TemplateDetail } from '../../../api'
import { invalidateAll } from '../../../lib/useLoadable'
import { NewStrategyDraftProvider } from '../NewStrategyDraft'
import { ReviewTemplate } from './ReviewTemplate'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const cspx = { id: 'id-CSPX', symbol: 'CSPX.L', name: 'iShares Core S&P 500', what: 'Tracks the 500 largest US companies' }

const trend = {
  key: 'trend',
  name: 'Index trend',
  tagline: '',
  rationale: '',
  rules: [],
  signals: 'buysAndSells',
  watches: { kind: 'fund', fund: cspx },
  backtest: null,
  defaults: { version: 1, budget: { amount: 3000, step: null, maxSteps: null }, params: {} },
  settings: [],
  settingGroups: [],
  aiRound: null,
} as unknown as TemplateDetail

function open() {
  vi.spyOn(api, 'getTemplate').mockResolvedValue(trend)
  render(
    <NewStrategyDraftProvider owner="a@example.com">
      <MemoryRouter initialEntries={['/new/templates/trend']}>
        <Routes>
          <Route path="/new/templates/:key" element={<ReviewTemplate />} />
        </Routes>
      </MemoryRouter>
    </NewStrategyDraftProvider>,
  )
}

describe('the fund picker', () => {
  it('replaces the tested fund with a searched one, adding it first, and goes back to the tested one', async () => {
    const search = vi.spyOn(api, 'searchAssets').mockResolvedValue([
      { id: null, symbol: 'VT', name: 'Vanguard Total World', exchange: 'ARCX', what: 'Vanguard Total World' },
    ])
    const add = vi.spyOn(api, 'addAsset').mockResolvedValue({
      asset: { id: 'id-VT', symbol: 'VT', name: 'Vanguard Total World', what: 'Vanguard Total World' },
      pricesIn: false,
    })
    open()

    fireEvent.click(await screen.findByRole('button', { name: 'Change' }))
    fireEvent.change(screen.getByLabelText('Search funds'), { target: { value: 'world' } })
    fireEvent.click(await screen.findByRole('button', { name: /Vanguard Total World/ }, { timeout: 2000 }))

    expect(search).toHaveBeenCalledWith('world', 'fund')
    expect(add).toHaveBeenCalledWith('VT', 'fund')
    expect(await screen.findByText('Not enough price history yet.')).toBeTruthy()
    expect(screen.getByText(/Tested with iShares Core S&P 500/)).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Use the tested fund' }))
    expect(screen.getByText('iShares Core S&P 500')).toBeTruthy()
    expect(screen.queryByText(/Tested with/)).toBeNull()
  })

  it('picks a stored fund without adding it', async () => {
    vi.spyOn(api, 'searchAssets').mockResolvedValueOnce([
      { id: 'id-QQQ', symbol: 'QQQ', name: 'Invesco QQQ Trust', exchange: 'XNAS', what: 'Tracks the Nasdaq-100' },
    ])
    const add = vi.spyOn(api, 'addAsset')
    open()

    fireEvent.click(await screen.findByRole('button', { name: 'Change' }))
    fireEvent.change(screen.getByLabelText('Search funds'), { target: { value: 'nasdaq' } })
    fireEvent.click(await screen.findByRole('button', { name: /Invesco QQQ Trust/ }, { timeout: 2000 }))

    expect(add).not.toHaveBeenCalled()
    expect(screen.getByText('Tracks the Nasdaq-100')).toBeTruthy()
  })

  it('asks to confirm the email before adding a new fund', async () => {
    vi.spyOn(api, 'searchAssets').mockResolvedValue([
      { id: null, symbol: 'VT', name: 'Vanguard Total World', exchange: 'ARCX', what: 'Vanguard Total World' },
    ])
    vi.spyOn(api, 'addAsset').mockRejectedValue(new ForbiddenError())
    open()

    fireEvent.click(await screen.findByRole('button', { name: 'Change' }))
    fireEvent.change(screen.getByLabelText('Search funds'), { target: { value: 'world' } })
    fireEvent.click(await screen.findByRole('button', { name: /Vanguard Total World/ }, { timeout: 2000 }))

    expect(await screen.findByText(/Confirm your email to add a new fund/)).toBeTruthy()
    expect(screen.getByText('iShares Core S&P 500')).toBeTruthy()
  })
})
