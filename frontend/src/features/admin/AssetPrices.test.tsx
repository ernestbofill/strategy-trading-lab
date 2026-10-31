// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, InvalidValueError, type AdminAsset, type AdminPrices } from '../../api'
import { AllPricesSection, AssetPricesActions } from './AssetPrices'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const asset = (id: string, symbol: string): AdminAsset => ({
  id,
  symbol,
  exchange: 'XNYS',
  name: symbol,
  what: '',
  kind: 'stock',
  currency: 'USD',
  active: true,
  strategies: 0,
  latestPriceDate: '2026-10-30',
  pricesProblem: null,
})

const assets = [asset('a1', 'ETN'), asset('a2', 'MOD')]

const prices = (running: boolean) => ({ running }) as AdminPrices

describe('AllPricesSection', () => {
  it('downloads every asset one by one, then the rates, and names what failed', async () => {
    vi.spyOn(api, 'getAdminPrices').mockResolvedValue(prices(false))
    const download = vi
      .spyOn(api, 'downloadAdminAssetPrices')
      .mockResolvedValueOnce(assets[0])
      .mockRejectedValueOnce(new InvalidValueError('Yahoo answered HTTP 429 for MOD.'))
    const rates = vi.spyOn(api, 'downloadAdminRates').mockResolvedValue({ rows: 500, problems: [] })
    const onChanged = vi.fn().mockResolvedValue(undefined)
    render(<AllPricesSection assets={[...assets, { ...asset('a3', 'OLD'), active: false }]} onChanged={onChanged} />)

    fireEvent.change(screen.getByLabelText('Years to download'), { target: { value: '3' } })
    fireEvent.click(screen.getByRole('button', { name: 'Download all' }))

    expect(await screen.findByText('Downloaded 1 of 2 assets and the exchange rates. Failed: MOD: Yahoo answered HTTP 429 for MOD.')).toBeTruthy()
    expect(download).toHaveBeenNthCalledWith(1, 'a1', 3)
    expect(download).toHaveBeenNthCalledWith(2, 'a2', 3)
    expect(download).toHaveBeenCalledTimes(2) // The hidden one is left out.
    expect(rates).toHaveBeenCalledWith(3)
    expect(onChanged).toHaveBeenCalled()
  })

  it("doesn't start while the daily refresh runs, or with years out of range", async () => {
    vi.spyOn(api, 'getAdminPrices').mockResolvedValue(prices(true))
    const download = vi.spyOn(api, 'downloadAdminAssetPrices')
    render(<AllPricesSection assets={assets} onChanged={vi.fn().mockResolvedValue(undefined)} />)

    fireEvent.click(screen.getByRole('button', { name: 'Download all' }))
    expect(await screen.findByText("The daily price refresh is running: try again when it's done.")).toBeTruthy()

    fireEvent.change(screen.getByLabelText('Years to download'), { target: { value: '0' } })
    fireEvent.click(screen.getByRole('button', { name: 'Download all' }))
    expect(await screen.findByText('Choose 1 to 30 years.')).toBeTruthy()
    expect(download).not.toHaveBeenCalled()
  })

  it('deletes every price only after a second click', async () => {
    const flush = vi.spyOn(api, 'flushAdminPrices').mockResolvedValue({ prices: 1200, rates: 300 })
    render(<AllPricesSection assets={assets} onChanged={vi.fn().mockResolvedValue(undefined)} />)

    fireEvent.click(screen.getByRole('button', { name: 'Delete all' }))
    expect(flush).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Sure? Delete all' }))

    expect(await screen.findByText('Deleted 1200 prices and 300 exchange rates.')).toBeTruthy()
  })
})

describe('AssetPricesActions', () => {
  it("downloads one asset's prices and deletes them after a second click", async () => {
    const download = vi.spyOn(api, 'downloadAdminAssetPrices').mockResolvedValue({ ...assets[0], latestPriceDate: '2026-10-31' })
    const flush = vi.spyOn(api, 'flushAdminAssetPrices').mockResolvedValue()
    render(<AssetPricesActions asset={assets[0]} onChanged={vi.fn().mockResolvedValue(undefined)} />)

    fireEvent.click(screen.getByRole('button', { name: 'Download prices' }))
    expect(await screen.findByText('Downloaded ETN, prices to 2026-10-31.')).toBeTruthy()
    expect(download).toHaveBeenCalledWith('a1', 2)

    fireEvent.click(screen.getByRole('button', { name: 'Delete prices' }))
    fireEvent.click(screen.getByRole('button', { name: 'Sure? Delete prices' }))
    expect(await screen.findByText('Deleted the prices of ETN.')).toBeTruthy()
    expect(flush).toHaveBeenCalledWith('a1')
  })
})
