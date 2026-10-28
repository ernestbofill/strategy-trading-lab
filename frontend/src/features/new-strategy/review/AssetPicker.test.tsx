// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, ForbiddenError, InvalidValueError, type Security, type TemplateDetail } from '../../../api'
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

const stock = (symbol: string): Security => ({ id: `id-${symbol}`, symbol, name: `${symbol} Inc.`, what: `What ${symbol} does` })

const dip = (starterList: Security[]) =>
  ({ ...trend, key: 'dip', name: 'Winners at a discount', watches: { kind: 'stocks', starterList } }) as unknown as TemplateDetail

function open(template = trend) {
  vi.spyOn(api, 'getTemplate').mockResolvedValue(template)
  render(
    <NewStrategyDraftProvider owner="a@example.com">
      <MemoryRouter initialEntries={[`/new/templates/${template.key}`]}>
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

describe('"+ Add a stock"', () => {
  const zscaler = { id: null, symbol: 'ZS', name: 'Zscaler, Inc.', exchange: 'XNAS', what: 'Technology · Software infrastructure' }

  async function search(text: string) {
    fireEvent.click(await screen.findByRole('button', { name: '+ Add a stock' }))
    fireEvent.change(screen.getByLabelText('Search stocks'), { target: { value: text } })
  }

  it('adds a searched stock at the end of the list, ticked', async () => {
    const find = vi.spyOn(api, 'searchAssets').mockResolvedValue([zscaler])
    const add = vi.spyOn(api, 'addAsset').mockResolvedValue({
      asset: { id: 'id-ZS', symbol: 'ZS', name: 'Zscaler, Inc.', what: 'Technology · Software infrastructure' },
      pricesIn: false,
    })
    open(dip([stock('NVDA'), stock('AVGO')]))

    await search('zscaler')
    fireEvent.click(await screen.findByRole('button', { name: /Zscaler, Inc\./ }, { timeout: 2000 }))

    expect(find).toHaveBeenCalledWith('zscaler', 'stock')
    expect(add).toHaveBeenCalledWith('ZS', 'stock')
    expect(await screen.findByText('Not enough price history yet.')).toBeTruthy()
    const boxes = screen.getAllByRole('checkbox')
    expect(boxes).toHaveLength(3)
    expect((boxes[2] as HTMLInputElement).checked).toBe(true)
    expect(screen.getByText('Zscaler, Inc.')).toBeTruthy()
    expect(screen.getByText('3 of 3 ticked')).toBeTruthy()
  })

  it('ticks a stock already in the list instead of adding it twice', async () => {
    vi.spyOn(api, 'searchAssets').mockResolvedValue([{ ...stock('AVGO'), exchange: 'XNAS' }])
    const add = vi.spyOn(api, 'addAsset')
    open(dip([stock('NVDA'), stock('AVGO')]))

    fireEvent.click(await screen.findByRole('checkbox', { name: /AVGO Inc\./ }))
    expect(screen.getByText('1 of 2 ticked')).toBeTruthy()
    await search('avgo')
    fireEvent.click(await screen.findByRole('button', { name: /AVGO Inc\./ }, { timeout: 2000 }))

    expect(add).not.toHaveBeenCalled()
    expect(screen.getAllByRole('checkbox')).toHaveLength(2)
    expect(screen.getByText('2 of 2 ticked')).toBeTruthy()
  })

  it("says so when nothing is found, and shows the server's reason at the user's limit", async () => {
    vi.spyOn(api, 'searchAssets').mockResolvedValueOnce([]).mockResolvedValue([zscaler])
    vi.spyOn(api, 'addAsset').mockRejectedValue(new InvalidValueError("You can add up to 30 new stocks and funds, and you've reached it."))
    open(dip([stock('NVDA')]))

    await search('zzzz')
    expect(await screen.findByText("Nothing found. Try the company's name or its symbol.", undefined, { timeout: 2000 })).toBeTruthy()

    fireEvent.change(screen.getByLabelText('Search stocks'), { target: { value: 'zscaler' } })
    fireEvent.click(await screen.findByRole('button', { name: /Zscaler, Inc\./ }, { timeout: 2000 }))

    expect(await screen.findByText("You can add up to 30 new stocks and funds, and you've reached it.")).toBeTruthy()
    expect(screen.getAllByRole('checkbox')).toHaveLength(1)
  })

  it('stops at 60 stocks', async () => {
    open(dip(Array.from({ length: 60 }, (_, i) => stock(`S${i}`))))

    expect(await screen.findByText('60 stocks, the most for now')).toBeTruthy()
    expect(screen.queryByRole('button', { name: '+ Add a stock' })).toBeNull()
  })
})
