// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, InvalidFieldsError, type AdminTemplate, type AdminTemplateVersion } from '../../api'
import { AdminTemplates } from './AdminTemplates'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const eaton = { id: 'a1', symbol: 'ETN', name: 'Eaton', active: true }
const vertiv = { id: 'a2', symbol: 'VRT', name: 'Vertiv', active: true }

const version = (over: Partial<AdminTemplateVersion>): AdminTemplateVersion => ({
  version: 1,
  active: false,
  createdAt: '2026-10-18T00:00:00Z',
  defaults: { zoneEndPct: 15 },
  assets: [eaton, vertiv],
  closePct: 3,
  strategies: 0,
  ...over,
})

const dip: AdminTemplate = {
  key: 'dip',
  name: 'Winners at a discount',
  investsIn: 'stockList',
  hasClosePct: true,
  settings: [{ key: 'zoneEndPct', sentence: 'The discount zone ends {value} below the peak', value: '{n}%', step: 1, min: 2, max: 40, below: null }],
  versions: [version({ version: 2, active: true, createdAt: '2026-11-08T09:00:00Z', defaults: { zoneEndPct: 12 }, strategies: 1 }), version({ strategies: 4 })],
}

const show = () =>
  render(
    <MemoryRouter>
      <AdminTemplates />
    </MemoryRouter>,
  )

describe('AdminTemplates', () => {
  it('saves the shown values as a new version, and shows a refused or blank value under its field', async () => {
    vi.spyOn(api, 'getAdminTemplates').mockResolvedValue([dip])
    const v3 = version({ version: 3, active: true, createdAt: '2026-11-08T10:00:00Z', defaults: { zoneEndPct: 12 }, assets: [eaton], closePct: 4 })
    const save = vi
      .spyOn(api, 'saveAdminTemplateVersion')
      .mockRejectedValueOnce(new InvalidFieldsError({ closePct: 'Use 0.5 to 10, in steps of 0.5.' }))
      .mockResolvedValueOnce({ ...dip, versions: [v3, { ...dip.versions[0], active: false }, dip.versions[1]] })
    show()

    // The active version is shown first.
    expect((await screen.findByLabelText<HTMLInputElement>('The discount zone ends _% below the peak')).value).toBe('12')

    // A blank is stopped on the page, never sent.
    fireEvent.change(screen.getByLabelText('Getting close: within _% of the buy rule'), { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save as new version' }))
    expect(screen.getByText('Type a number.')).toBeTruthy()
    expect(save).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Remove Vertiv' }))
    fireEvent.change(screen.getByLabelText('Getting close: within _% of the buy rule'), { target: { value: '11' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save as new version' }))
    expect(await screen.findByText('Use 0.5 to 10, in steps of 0.5.')).toBeTruthy()
    expect(save).toHaveBeenCalledWith('dip', { defaults: { zoneEndPct: 12 }, assetIds: ['a1'], closePct: 11 })

    fireEvent.change(screen.getByLabelText('Getting close: within _% of the buy rule'), { target: { value: '4' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save as new version' }))
    expect(await screen.findByText('v3 is active. New strategies start with it.')).toBeTruthy()
    expect(screen.getByText('v3 active')).toBeTruthy()
  })

  it('shows an older version and makes it active again', async () => {
    vi.spyOn(api, 'getAdminTemplates').mockResolvedValue([dip])
    const activate = vi
      .spyOn(api, 'activateAdminTemplateVersion')
      .mockResolvedValue({ ...dip, versions: [{ ...dip.versions[0], active: false }, { ...dip.versions[1], active: true }] })
    show()

    fireEvent.change(await screen.findByLabelText('Version'), { target: { value: '1' } })
    expect(screen.getByLabelText<HTMLInputElement>('The discount zone ends _% below the peak').value).toBe('15')
    fireEvent.click(screen.getByRole('button', { name: 'Make v1 active' }))

    expect(await screen.findByText('v1 is active. New strategies start with it.')).toBeTruthy()
    expect(activate).toHaveBeenCalledWith('dip', 1)
    expect(screen.queryByRole('button', { name: 'Make v1 active' })).toBeNull()
  })
})
