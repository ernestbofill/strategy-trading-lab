// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type AdminAsset } from '../../api'
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

    fireEvent.click(screen.getByRole('button', { name: 'Hide Eaton' }))
    expect(setActive).toHaveBeenCalledWith('a1', false)
    expect(await screen.findByRole('button', { name: 'Show Eaton' })).toBeTruthy()
  })
})
