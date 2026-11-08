// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type Account, type Role } from '../../api'
import { AccountPage } from './AccountPage'
import { SettingsPage } from './SettingsPage'

let currentRole: Role = 'user'

const baseAccount: Account = {
  email: 'user@example.com',
  firstName: null, lastName: null, emailVerified: true,
  locale: 'en',
  timeZone: 'Europe/Madrid',
  overviewEmail: true,
  overviewPush: true,
  alertEmails: true, alertPush: true,
  currency: 'EUR',
  role: 'user',
  aiSharing: false, notifyHour: 7,
}

const push = vi.hoisted(() => ({ offered: false }))
vi.mock('./usePushOffered', () => ({ usePushOffered: () => push.offered }))
vi.mock('./DevicePush', () => ({ DevicePush: () => <p>Device state</p> }))

vi.mock('./AccountContext', () => ({
  useCurrentAccount: () => ({ ...baseAccount, role: currentRole }),
  useAccount: () => ({ clear: vi.fn(), refresh: vi.fn() }),
  useIsOwner: () => currentRole === 'owner',
  useRole: () => currentRole,
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

  it('deletes the account only after the password, and not for an owner', async () => {
    currentRole = 'user'
    const remove = vi.spyOn(api, 'deleteAccount').mockResolvedValue()
    render(
      <MemoryRouter>
        <AccountPage />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Delete my account' }))
    const confirm = screen.getByRole('button', { name: 'Delete for good' }) as HTMLButtonElement
    expect(confirm.disabled).toBe(true)
    fireEvent.change(screen.getByLabelText('Your password'), { target: { value: 'a long password' } })
    fireEvent.click(confirm)
    await waitFor(() => expect(remove).toHaveBeenCalledWith('a long password'))

    cleanup()
    currentRole = 'owner'
    render(
      <MemoryRouter>
        <AccountPage />
      </MemoryRouter>,
    )
    expect(screen.queryByRole('button', { name: 'Delete my account' })).toBeNull()
  })
})

describe('SettingsPage', () => {
  it('sets signals and reminders by app and email, and the summary by app and email, in one place', async () => {
    currentRole = 'user'
    push.offered = true
    const setPush = vi.spyOn(api, 'setAlertPush').mockResolvedValue()
    const setSummaryEmail = vi.spyOn(api, 'setOverviewEmail').mockResolvedValue()
    const setSummaryPush = vi.spyOn(api, 'setOverviewPush').mockResolvedValue()
    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    )

    const alerts = screen.getByRole('group', { name: 'Signals and reminders' })
    const summary = screen.getByRole('group', { name: 'Summary' })
    expect(within(alerts).getAllByRole('checkbox')).toHaveLength(2)
    expect(within(summary).getAllByRole('checkbox')).toHaveLength(2)
    fireEvent.click(within(alerts).getByRole('checkbox', { name: 'App' }))
    fireEvent.click(within(summary).getByRole('checkbox', { name: 'App' }))
    fireEvent.click(within(summary).getByRole('checkbox', { name: 'Email' }))
    await waitFor(() => expect(setPush).toHaveBeenCalledWith(false))
    await waitFor(() => expect(setSummaryPush).toHaveBeenCalledWith(false))
    await waitFor(() => expect(setSummaryEmail).toHaveBeenCalledWith(false))
  })

  it('opens Account from its first row and names Spain after Barcelona', () => {
    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: /user@example.com/ }).getAttribute('href')).toBe('/account')
    expect((screen.getByRole('combobox', { name: 'Time zone' }) as HTMLSelectElement).selectedOptions[0].textContent).toMatch(/· Barcelona, Paris, Copenhagen/)
  })

  it('sends notifications at 07:00 by default, or as soon as they are ready', async () => {
    const setHour = vi.spyOn(api, 'setNotifyHour').mockResolvedValue()
    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    )

    const hour = screen.getByRole('combobox', { name: 'Notification time' }) as HTMLSelectElement
    expect(hour.selectedOptions[0].textContent).toBe('07:00')
    fireEvent.change(hour, { target: { value: 'asap' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save time' }))
    await waitFor(() => expect(setHour).toHaveBeenCalledWith(null))
  })

  it('leaves the App box out where the app offers no push', () => {
    currentRole = 'user'
    push.offered = false
    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    )

    const alerts = screen.getByRole('group', { name: 'Signals and reminders' })
    expect(within(alerts).queryByRole('checkbox', { name: 'App' })).toBeNull()
    expect(within(alerts).getByRole('checkbox', { name: 'Email' })).toBeTruthy()
    expect(screen.queryByText('This device')).toBeNull()
  })
})
