// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type Account } from '../api'
import { AccountProvider } from '../features/account/AccountContext'
import { PrimaryAction, Screen, TopBar } from './Screen'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const makeAccount = (role: 'user' | 'owner'): Account => ({
  email: 'test@example.com',
  firstName: null, lastName: null, emailVerified: true,
  locale: 'en',
  timeZone: 'UTC',
  overviewEmail: true, alertEmails: true, alertPush: true,
  currency: 'EUR',
  role,
  aiSharing: false,
})

describe('TopBar', () => {
  it('renders title as a link when titleLink is provided', () => {
    render(
      <MemoryRouter>
        <TopBar title="STL // ADMIN" titleLink="/" />
      </MemoryRouter>,
    )

    const link = screen.getByRole('link', { name: 'STL // ADMIN, home' })
    expect(link.getAttribute('href')).toBe('/')
  })

  it('renders title as plain div when titleLink is not provided', () => {
    render(
      <MemoryRouter>
        <TopBar title="STL // LOG IN" />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('link', { name: 'STL // LOG IN' })).toBeNull()
    expect(screen.getByText('STL // LOG IN')).toBeDefined()
  })

  it('goes up and names the parent on a screen opened directly, without looping back', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(makeAccount('user'))
    render(
      <MemoryRouter initialEntries={['/strategies/s1/signals/g1']}>
        <AccountProvider>
          <Routes>
            <Route path="/" element={<p>Dashboard page</p>} />
            <Route
              path="/strategies/:id"
              element={<TopBar back="/" />}
            />
            <Route path="/strategies/:id/signals/:sid" element={<TopBar back="/strategies/s1" />} />
          </Routes>
        </AccountProvider>
      </MemoryRouter>,
    )

    // Opened from a notification: up to the strategy, which goes on up to the dashboard, not back to the signal.
    fireEvent.click(screen.getByRole('link', { name: '\u2190 STRATEGY' }))
    fireEvent.click(await screen.findByRole('link', { name: '\u2190 DASHBOARD' }))
    expect(screen.getByText('Dashboard page')).toBeDefined()
  })

  it('goes back in the history when the screen was opened from inside the app', () => {
    render(
      <MemoryRouter initialEntries={['/strategies/s1']}>
        <Routes>
          <Route path="/strategies/:id" element={<Link to="/strategies/s1/signals/g1">Open signal</Link>} />
          <Route path="/strategies/:id/signals/:sid" element={<TopBar back="/strategies/s1" />} />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('link', { name: 'Open signal' }))
    fireEvent.click(screen.getByRole('link', { name: '\u2190 BACK' }))
    expect(screen.getByRole('link', { name: 'Open signal' })).toBeDefined()
  })

  it('shows FE version as a link to /admin only when user is an owner', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(makeAccount('owner'))

    render(
      <MemoryRouter>
        <AccountProvider>
          <TopBar title="STL // DASHBOARD" />
        </AccountProvider>
      </MemoryRouter>,
    )

    const versionLink = await screen.findByRole('link', { name: new RegExp(`v${__APP_VERSION__}`) })
    expect(versionLink.getAttribute('href')).toBe('/admin')
  })

  it('does not show FE version link when user is not an owner', async () => {
    const getAccountSpy = vi.spyOn(api, 'getAccount').mockResolvedValue(makeAccount('user'))

    render(
      <MemoryRouter>
        <AccountProvider>
          <TopBar title="STL // DASHBOARD" />
        </AccountProvider>
      </MemoryRouter>,
    )

    await waitFor(() => expect(getAccountSpy).toHaveBeenCalled())
    expect(screen.queryByRole('link', { name: new RegExp(`v${__APP_VERSION__}`) })).toBeNull()
    expect(screen.queryByText(new RegExp(`v${__APP_VERSION__}`))).toBeNull()
  })
})

describe('Screen', () => {
  it('ends the content with the action and its note, above the site footer', () => {
    render(
      <MemoryRouter>
        <Screen top={<TopBar title="STL" />} action={<PrimaryAction onClick={() => {}}>Continue</PrimaryAction>} note="A hint">
          <p>Body</p>
        </Screen>
      </MemoryRouter>,
    )

    const main = screen.getByRole('main')
    const action = screen.getByRole('button', { name: 'Continue' })
    const footer = screen.getByRole('contentinfo')
    expect(main.contains(action)).toBe(true)
    expect(main.contains(screen.getByText('A hint'))).toBe(true)
    expect(action.compareDocumentPosition(footer) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })
})
