// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type AdminAlert } from '../../api'
import { AdminAlerts } from './AdminAlerts'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const alert = (over: Partial<AdminAlert>): AdminAlert => ({
  id: 'a1',
  source: 'strategies',
  status: 'open',
  title: '"Tech dips" is missing price data',
  detail: 'friend@example.com · dip · live\nSAP.DE: the newest price is from 2026-10-20.',
  count: 3,
  firstSeenAt: '2026-10-29T22:00:00Z',
  lastSeenAt: '2026-10-31T22:00:00Z',
  closedAt: null,
  ...over,
})

const show = () =>
  render(
    <MemoryRouter>
      <AdminAlerts />
    </MemoryRouter>,
  )

describe('AdminAlerts', () => {
  it('groups the alerts by status and dismisses an open one', async () => {
    const alerts = [
      alert({}),
      alert({ id: 'a2', source: 'prices', status: 'dismissed', title: 'XYZ: prices weren\'t updated', detail: null, closedAt: '2026-10-30T08:00:00Z' }),
      alert({ id: 'a3', source: 'email', status: 'cleared', title: 'Account emails failed', closedAt: '2026-10-31T09:00:00Z' }),
    ]
    vi.spyOn(api, 'getAdminAlerts').mockResolvedValueOnce(alerts).mockResolvedValueOnce([{ ...alerts[0], status: 'dismissed' }, alerts[1], alerts[2]])
    const dismiss = vi.spyOn(api, 'dismissAdminAlert').mockResolvedValue()
    show()

    const open = (await screen.findByRole('heading', { name: 'Open · 1' })).closest('section')!
    expect(within(open).getByText('"Tech dips" is missing price data')).toBeTruthy()
    expect(within(open).getByText(/SAP\.DE: the newest price/)).toBeTruthy()
    expect(within(open).getByText(/· 3×$/)).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Dismissed · 1' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Cleared this week' })).toBeTruthy()

    fireEvent.click(within(open).getByRole('button', { name: 'Dismiss' }))
    expect(await screen.findByRole('heading', { name: 'Open · 0' })).toBeTruthy()
    expect(dismiss).toHaveBeenCalledWith('a1')
    expect(screen.getByText('Nothing open.')).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Dismissed · 2' })).toBeTruthy()
  })

  it('shows no error when a dismiss is refused because the alert is no longer open', async () => {
    vi.spyOn(api, 'getAdminAlerts').mockResolvedValueOnce([alert({})]).mockResolvedValueOnce([alert({ status: 'cleared', closedAt: '2026-11-01T08:00:00Z' })])
    vi.spyOn(api, 'dismissAdminAlert').mockRejectedValue(new Error('HTTP 404'))
    show()

    fireEvent.click(await screen.findByRole('button', { name: 'Dismiss' }))
    expect(await screen.findByRole('heading', { name: 'Open · 0' })).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('says so when the alerts do not load', async () => {
    vi.spyOn(api, 'getAdminAlerts').mockRejectedValue(new Error('HTTP 500'))
    show()
    expect(await screen.findByText('Could not load the alerts. Try again later.')).toBeTruthy()
  })
})
