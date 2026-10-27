// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Account, Role } from '../../api'
import { AccountPage } from './AccountPage'

let currentRole: Role = 'user'

const baseAccount: Account = {
  email: 'user@example.com',
  emailVerified: true,
  locale: 'en',
  timeZone: 'Europe/Madrid',
  overviewEmail: true,
  currency: 'EUR',
  role: 'user',
  aiSharing: false,
}

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
})
