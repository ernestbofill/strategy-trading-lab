// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type Account, type Role } from '../../api'
import { AccountPage } from './AccountPage'

let currentRole: Role = 'user'

const baseAccount: Account = {
  email: 'user@example.com',
  firstName: null, lastName: null, emailVerified: true,
  locale: 'en',
  timeZone: 'Europe/Madrid',
  overviewEmail: true,
  alertEmails: true, alertPush: true,
  currency: 'EUR',
  role: 'user',
  aiSharing: false,
}

const push = vi.hoisted(() => ({ offered: false }))
vi.mock('./usePushOffered', () => ({ usePushOffered: () => push.offered }))
vi.mock('./DevicePush', () => ({ DevicePush: () => <p>Device state</p> }))

vi.mock('./AccountContext', () => ({
  useCurrentAccount: () => ({ ...baseAccount, role: currentRole }),
  useAccount: () => ({ clear: vi.fn(), refresh: vi.fn() }),
  useIsOwner: () => currentRole === 'owner',
}))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe('AccountPage', () => {
  it('renders the admin link when the user is an owner', () => {
    currentRole = 'owner'
    render(
      <MemoryRouter>
        <AccountPage />
      </MemoryRouter>,
    )

    const adminLink = screen.getByRole('link', { name: /Admin settings/i })
    expect(adminLink).toBeTruthy()
    expect(adminLink.getAttribute('href')).toBe('/admin')
  })

  it('does not render the admin link when the user is not an owner', () => {
    currentRole = 'user'
    render(
      <MemoryRouter>
        <AccountPage />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('link', { name: /Admin settings/i })).toBeNull()
  })

  it('sets signals and reminders by app and email, and the summary by email, in one place', async () => {
    currentRole = 'user'
    push.offered = true
    const setPush = vi.spyOn(api, 'setAlertPush').mockResolvedValue()
    const setSummary = vi.spyOn(api, 'setOverviewEmail').mockResolvedValue()
    render(
      <MemoryRouter>
        <AccountPage />
      </MemoryRouter>,
    )

    const alerts = screen.getByRole('group', { name: 'Signals and reminders' })
    const summary = screen.getByRole('group', { name: 'Summary' })
    expect(within(alerts).getAllByRole('checkbox')).toHaveLength(2)
    expect(within(summary).getAllByRole('checkbox')).toHaveLength(1)
    fireEvent.click(within(alerts).getByRole('checkbox', { name: 'App' }))
    fireEvent.click(within(summary).getByRole('checkbox', { name: 'Email' }))
    await waitFor(() => expect(setPush).toHaveBeenCalledWith(false))
    await waitFor(() => expect(setSummary).toHaveBeenCalledWith(false))
  })

  it('leaves the App box out where the app offers no push', () => {
    currentRole = 'user'
    push.offered = false
    render(
      <MemoryRouter>
        <AccountPage />
      </MemoryRouter>,
    )

    const alerts = screen.getByRole('group', { name: 'Signals and reminders' })
    expect(within(alerts).queryByRole('checkbox', { name: 'App' })).toBeNull()
    expect(within(alerts).getByRole('checkbox', { name: 'Email' })).toBeTruthy()
    expect(screen.queryByText('This device')).toBeNull()
  })
})
