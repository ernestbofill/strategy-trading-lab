// @vitest-environment jsdom
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
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
  overviewEmail: true,
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

  it('shows FE version only when user is an owner', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(makeAccount('owner'))

    render(
      <MemoryRouter>
        <AccountProvider>
          <TopBar title="STL // ADMIN" />
        </AccountProvider>
      </MemoryRouter>,
    )

    expect(await screen.findByText(new RegExp(`v${__APP_VERSION__}`))).toBeDefined()
  })

  it('does not show FE version when user is not an owner', async () => {
    const getAccountSpy = vi.spyOn(api, 'getAccount').mockResolvedValue(makeAccount('user'))

    render(
      <MemoryRouter>
        <AccountProvider>
          <TopBar title="STL // DASHBOARD" />
        </AccountProvider>
      </MemoryRouter>,
    )

    await waitFor(() => expect(getAccountSpy).toHaveBeenCalled())
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
