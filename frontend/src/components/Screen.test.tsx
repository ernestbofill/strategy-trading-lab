// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
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
  it('shows a plain title with no right side on a screen like Log in', () => {
    render(
      <MemoryRouter>
        <TopBar title="STL // LOG IN" />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('link')).toBeNull()
    expect(screen.getByText('STL // LOG IN')).toBeDefined()
  })

  it('links home with no arrow, and to the account on the right, for a signed-in user', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(makeAccount('user'))
    render(
      <MemoryRouter>
        <AccountProvider>
          <TopBar home />
        </AccountProvider>
      </MemoryRouter>,
    )

    expect((await screen.findByRole('link', { name: 'DASHBOARD' })).getAttribute('href')).toBe('/')
    expect(screen.getByRole('link', { name: 'ACCOUNT' }).getAttribute('href')).toBe('/account')
  })

  it('names home WELCOME and offers log in, back to this page, for a guest', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(null)
    render(
      <MemoryRouter initialEntries={['/legal/terms']}>
        <AccountProvider>
          <TopBar home />
        </AccountProvider>
      </MemoryRouter>,
    )

    expect((await screen.findByRole('link', { name: 'WELCOME' })).getAttribute('href')).toBe('/')
    expect(screen.getByRole('link', { name: 'LOG IN' }).getAttribute('href')).toBe('/log-in?next=%2Flegal%2Fterms')
  })

  it('goes up to the named parent, whatever screen came before', () => {
    render(
      <MemoryRouter initialEntries={['/', '/strategies/s1/signals/g1']} initialIndex={1}>
        <Routes>
          <Route path="/" element={<p>Dashboard page</p>} />
          <Route path="/strategies/:id" element={<p>Strategy page</p>} />
          <Route path="/strategies/:id/signals/:sid" element={<TopBar up="/strategies/s1" upLabel="common.up.strategy" aside={null} />} />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('link', { name: 'STRATEGY' }))
    expect(screen.getByText('Strategy page')).toBeDefined()
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
    // The version is in the footer, for anyone, not in the top bar.
    expect(footer.textContent).toContain(`v${__APP_VERSION__}`)
  })
})
