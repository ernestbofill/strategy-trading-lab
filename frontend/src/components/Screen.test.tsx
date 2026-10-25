// @vitest-environment jsdom
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type Account } from '../api'
import { AccountProvider } from '../features/account/AccountContext'
import { TopBar } from './Screen'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const makeAccount = (role: 'user' | 'owner'): Account => ({
  email: 'test@example.com',
  emailVerified: true,
  locale: 'en',
  timeZone: 'UTC',
  overviewEmail: true,
  currency: 'EUR',
  role,
})

describe('TopBar', () => {
  it('renders title as a link when titleLink is provided', () => {
    render(
      <MemoryRouter>
        <TopBar title="STL // ADMIN" titleLink="/" />
      </MemoryRouter>,
    )

    const link = screen.getByRole('link', { name: 'STL // ADMIN' })
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
