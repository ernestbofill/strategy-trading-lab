// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, InvalidFieldsError, type AdminAsset } from '../../api'
import { AdminAssets } from './AdminAssets'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const assets: AdminAsset[] = [
  {
    id: 'a1',
    symbol: 'ETN',
    exchange: 'XNYS',
    name: 'Eaton',
    what: 'Electrical power equipment',
    kind: 'stock',
    currency: 'USD',
    active: true,
    strategies: 2,
    latestPriceDate: '2026-10-22',
    pricesProblem: null,
  },
  {
    id: 'a2',
    symbol: 'SPY',
    exchange: 'ARCX',
    name: 'SPDR S&P 500 ETF',
    what: 'Tracks the 500 largest US companies',
    kind: 'fund',
    currency: 'USD',
    active: false,
    strategies: 0,
    latestPriceDate: null,
    pricesProblem: 'Yahoo sent 3mo bars for SPY instead of daily ones.',
  },
]

describe('AdminAssets', () => {
  it('loads the assets into the data table and toggles whether an asset is active', async () => {
    vi.spyOn(api, 'getAdminAssets')
      .mockResolvedValueOnce(assets)
      .mockResolvedValueOnce([{ ...assets[0], active: false }, assets[1]])
    const setActive = vi.spyOn(api, 'setAdminAssetActive').mockResolvedValue()

    render(
      <MemoryRouter initialEntries={['/admin/assets']}>
        <AdminAssets />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Electrical power equipment')).toBeTruthy()
    expect(screen.getByText('ETN')).toBeTruthy()
    expect(screen.getByText('2026-10-22')).toBeTruthy()
    // A failed price fetch is shown on the asset, not only in the server log.
    expect(screen.getByText('Yahoo sent 3mo bars for SPY instead of daily ones.')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Hide Eaton' }))
    expect(setActive).toHaveBeenCalledWith('a1', false)
    expect(await screen.findByRole('button', { name: 'Show Eaton' })).toBeTruthy()
  })

  const renderPage = () =>
    render(
      <MemoryRouter initialEntries={['/admin/assets']}>
        <AdminAssets />
      </MemoryRouter>,
    )

  it('adds an asset by its symbol, says what it did and reloads the list', async () => {
    const apple: AdminAsset = { ...assets[0], id: 'a3', symbol: 'AAPL', exchange: 'XNAS', name: 'Apple Inc.', what: 'Technology · Consumer electronics' }
    vi.spyOn(api, 'getAdminAssets').mockResolvedValueOnce(assets).mockResolvedValueOnce([...assets, apple])
    const add = vi
      .spyOn(api, 'addAdminAsset')
      .mockResolvedValueOnce({ asset: apple, created: true, problem: null })
      .mockRejectedValueOnce(new InvalidFieldsError({ symbol: 'Yahoo has no stock, fund or index with the symbol NOPE.' }))
    renderPage()

    fireEvent.change(await screen.findByLabelText('Yahoo symbol'), { target: { value: 'aapl' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByText('Added Apple Inc. (AAPL), prices to 2026-10-22.')).toBeTruthy()
    expect(add).toHaveBeenCalledWith('aapl')
    expect(await screen.findByText('Technology · Consumer electronics')).toBeTruthy()

    fireEvent.change(screen.getByLabelText('Yahoo symbol'), { target: { value: 'nope' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))
    expect(await screen.findByText('Yahoo has no stock, fund or index with the symbol NOPE.')).toBeTruthy()
  })

  it('says when the prices of an added asset did not load', async () => {
    vi.spyOn(api, 'getAdminAssets').mockResolvedValue(assets)
    vi.spyOn(api, 'addAdminAsset').mockResolvedValue({ asset: { ...assets[0], latestPriceDate: null }, created: true, problem: 'Yahoo answered HTTP 429 for ETN.' })
    renderPage()

    fireEvent.change(await screen.findByLabelText('Yahoo symbol'), { target: { value: 'ETN' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByText("Added Eaton (ETN), but its prices didn't load: Yahoo answered HTTP 429 for ETN. Add it again to retry.")).toBeTruthy()
  })

  it('says when an asset already there is hidden, and a failed reload apart from the add', async () => {
    vi.spyOn(api, 'getAdminAssets').mockResolvedValueOnce(assets).mockRejectedValueOnce(new Error('down'))
    vi.spyOn(api, 'addAdminAsset').mockResolvedValue({ asset: assets[1], created: false, problem: null })
    renderPage()

    fireEvent.change(await screen.findByLabelText('Yahoo symbol'), { target: { value: 'SPY' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByText('SPDR S&P 500 ETF (SPY) is already here, hidden: Show it to use it.')).toBeTruthy()
    expect(await screen.findByText("Couldn't reload the assets. Reload the page.")).toBeTruthy()
    expect(screen.queryByText("Couldn't reach the server. Try again.")).toBeNull()
  })

  it('edits the name and description of an asset', async () => {
    vi.spyOn(api, 'getAdminAssets')
      .mockResolvedValueOnce(assets)
      .mockResolvedValueOnce([{ ...assets[0], what: 'Power equipment' }, assets[1]])
    const update = vi
      .spyOn(api, 'updateAdminAsset')
      .mockRejectedValueOnce(new InvalidFieldsError({ name: 'Type something.' }))
      .mockResolvedValueOnce()
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Edit Eaton' }))
    const form = screen.getByRole('heading', { name: 'Edit ETN' }).closest('section')!
    fireEvent.change(within(form).getByLabelText('Name'), { target: { value: '' } })
    fireEvent.click(within(form).getByRole('button', { name: 'Save' }))
    expect(await within(form).findByText('Type something.')).toBeTruthy()

    fireEvent.change(within(form).getByLabelText('Name'), { target: { value: 'Eaton' } })
    fireEvent.change(within(form).getByLabelText('What it does'), { target: { value: 'Power equipment' } })
    fireEvent.click(within(form).getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Power equipment')).toBeTruthy()
    expect(update).toHaveBeenLastCalledWith('a1', { name: 'Eaton', what: 'Power equipment' })
    expect(screen.queryByRole('heading', { name: 'Edit ETN' })).toBeNull()
  })
})
