// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, InvalidFieldsError, type AdminTemplate } from '../../api'
import { AdminTemplates } from './AdminTemplates'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const dip: AdminTemplate = {
  key: 'dip',
  name: 'Winners at a discount',
  investsIn: 'stockList',
  changed: false,
  settings: [
    { key: 'zoneEndPct', sentence: 'The discount zone ends {value} below the peak', value: '{n}%', step: 1, min: 2, max: 40, below: null, tested: 15, current: 15 },
  ],
  assets: [
    { id: 'a1', symbol: 'ETN', name: 'Eaton', active: true },
    { id: 'a2', symbol: 'VRT', name: 'Vertiv', active: true },
  ],
  testedAssetIds: ['a1', 'a2'],
  closePct: 3,
  testedClosePct: 3,
}

describe('AdminTemplates', () => {
  it('saves the defaults, the starter list and getting close, and shows a refused value under its field', async () => {
    vi.spyOn(api, 'getAdminTemplates').mockResolvedValue([dip])
    const save = vi
      .spyOn(api, 'setAdminTemplate')
      .mockRejectedValueOnce(new InvalidFieldsError({ closePct: 'Use 0.5 to 10, in steps of 0.5.' }))
      .mockResolvedValueOnce({ ...dip, changed: true })
    render(
      <MemoryRouter>
        <AdminTemplates />
      </MemoryRouter>,
    )

    fireEvent.change(await screen.findByLabelText('The discount zone ends _% below the peak'), { target: { value: '12' } })
    fireEvent.click(screen.getByRole('button', { name: 'Remove Vertiv' }))
    fireEvent.change(screen.getByLabelText('Getting close: within _% of the buy rule'), { target: { value: '11' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Use 0.5 to 10, in steps of 0.5.')).toBeTruthy()
    expect(save).toHaveBeenCalledWith('dip', { defaults: { zoneEndPct: 12 }, assetIds: ['a1'], closePct: 11 })

    fireEvent.change(screen.getByLabelText('Getting close: within _% of the buy rule'), { target: { value: '4' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Saved. New strategies start with these.')).toBeTruthy()
    expect(screen.getByText('Changed')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Back to tested' })).toBeTruthy()
  })
})
