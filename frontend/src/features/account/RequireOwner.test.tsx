// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Role } from '../../api'
import { RequireOwner } from './RequireOwner'

afterEach(cleanup)

let role: Role = 'owner'
vi.mock('./AccountContext', () => ({ useCurrentAccount: () => ({ role }) }))

function open(as: Role) {
  role = as
  render(
    <MemoryRouter initialEntries={['/admin']}>
      <Routes>
        <Route path="/" element={<p>Start page</p>} />
        <Route element={<RequireOwner />}>
          <Route path="/admin" element={<p>Admin page</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireOwner', () => {
  it('shows the owner the page', () => {
    open('owner')
    expect(screen.getByText('Admin page')).toBeTruthy()
  })

  it.each<Role>(['user', 'vip'])('sends a %s to the start page', (as) => {
    open(as)
    expect(screen.getByText('Start page')).toBeTruthy()
    expect(screen.queryByText('Admin page')).toBeNull()
  })
})
