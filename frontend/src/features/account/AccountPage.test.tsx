// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
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
  alertEmails: true,
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

  it('lists running strategies for stock changes and turns one on', async () => {
    currentRole = 'user'
    vi.spyOn(api, 'getStockChangeEmails').mockResolvedValue([
      { strategyId: 's1', name: 'Index trend', mode: 'live', enabled: false },
    ])
    const set = vi.spyOn(api, 'setStockChangeEmails').mockResolvedValue()
    render(
      <MemoryRouter>
        <AccountPage />
      </MemoryRouter>,
    )

    const box = await screen.findByRole('checkbox', { name: /Index trend/ })
    expect(screen.getByText('Stock changes in the overview')).toBeTruthy()
    fireEvent.click(box)
    await waitFor(() => expect(set).toHaveBeenCalledWith('s1', true))
  })
})
