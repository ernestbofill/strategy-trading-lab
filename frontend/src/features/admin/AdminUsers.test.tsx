// @vitest-environment jsdom
import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type AdminUsers as AdminUsersPage } from '../../api'
import { AdminUsers } from './AdminUsers'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const page: AdminUsersPage = {
  counts: {
    users: 2,
    confirmed: 1,
    running: [
      { templateKey: 'dip', templateName: 'Winners at a discount', simulation: 3, live: 1 },
      { templateKey: 'bear', templateName: 'Bear markets', simulation: 0, live: 0 },
    ],
  },
  users: [
    {
      id: 'u2', email: 'new@example.com', role: 'user', joined: '2026-10-29T10:00:00Z', confirmed: false, lastVisit: null,
      running: 0, archived: 0,
    },
    {
      id: 'u1', email: 'friend@example.com', role: 'vip', joined: '2026-10-20T10:00:00Z', confirmed: true,
      lastVisit: '2026-10-28T10:00:00Z', running: 2, archived: 1,
    },
  ],
}

describe('AdminUsers', () => {
  it('shows the counts by template and mode, and every account with its strategies', async () => {
    vi.spyOn(api, 'getAdminUsers').mockResolvedValue(page)
    render(
      <MemoryRouter>
        <AdminUsers />
      </MemoryRouter>,
    )

    const running = await screen.findByRole('table', { name: 'Running strategies' })
    const dip = within(running).getByRole('row', { name: /Winners at a discount/ })
    expect(within(dip).getAllByRole('cell').map((c) => c.textContent)).toEqual(['3', '1'])
    expect(screen.getByText('Accounts').nextElementSibling?.textContent).toBe('2')
    expect(screen.getByText('Confirmed', { selector: 'dt' }).nextElementSibling?.textContent).toBe('1')

    expect(screen.getByText('friend@example.com')).toBeTruthy()
    expect(screen.getByText('VIP')).toBeTruthy()
    expect(screen.getByText('new@example.com')).toBeTruthy()
  })

  it('says so when the users do not load', async () => {
    vi.spyOn(api, 'getAdminUsers').mockRejectedValue(new Error('HTTP 500'))
    render(
      <MemoryRouter>
        <AdminUsers />
      </MemoryRouter>,
    )
    expect(await screen.findByText('Could not load the users. Try again later.')).toBeTruthy()
  })
})
