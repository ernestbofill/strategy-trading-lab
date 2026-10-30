// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type Account } from './api'
import App from './App'

vi.mock('./features/dashboard/Dashboard', () => ({ Dashboard: () => <p>Dashboard screen</p> }))
vi.mock('./features/new-strategy/PickTemplate', () => ({ PickTemplate: () => <p>Pick template screen</p> }))

const account: Account = {
  email: 'user@example.com',
  emailVerified: false,
  locale: 'en',
  timeZone: 'UTC',
  overviewEmail: true,
  currency: 'EUR',
  role: 'user',
  aiSharing: false,
}

function open(path: string) {
  window.history.replaceState({}, '', path)
  render(<App />)
}

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  window.history.replaceState({}, '', '/')
})

describe('app routes', () => {
  it('opens the dashboard at the home route', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(account)

    open('/')

    expect(await screen.findByText('Dashboard screen')).toBeTruthy()
    expect(screen.queryByText('Pick template screen')).toBeNull()
  })

  it('keeps the dashboard route working', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(account)

    open('/dashboard')

    expect(await screen.findByText('Dashboard screen')).toBeTruthy()
  })

  it('keeps the template picker on the new-strategy route', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(account)

    open('/new')

    expect(await screen.findByText('Pick template screen')).toBeTruthy()
  })

  it('goes to the template picker after sign-up', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValueOnce(null).mockResolvedValueOnce(account)
    vi.spyOn(api, 'signUp').mockResolvedValue()

    open('/sign-up')

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'user@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'a-secure-password' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Not financial advice/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Create my account' }))

    expect(await screen.findByText('Pick template screen')).toBeTruthy()
    expect(screen.queryByText('Dashboard screen')).toBeNull()
  })

  it('lets guests view the template picker at /new without an account', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(null)

    open('/new')

    expect(await screen.findByText('Pick template screen')).toBeTruthy()
  })

  it('offers Skip for now, look around on sign-up, leading to /new', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(null)

    open('/sign-up')

    const skipLink = screen.getByRole('link', { name: 'Skip for now, look around' })
    expect(skipLink.getAttribute('href')).toBe('/new')
    fireEvent.click(skipLink)

    expect(await screen.findByText('Pick template screen')).toBeTruthy()
  })

  it('renders legal links in the footer on every screen', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(null)

    open('/sign-up')

    expect(screen.getByRole('link', { name: 'Legal notice' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Terms of use' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Privacy policy' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Disclaimer' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'About' })).toBeTruthy()
  })

  it('renders public legal pages such as /legal/disclaimer', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValue(null)

    open('/legal/disclaimer')

    expect(await screen.findByRole('heading', { level: 1, name: 'Disclaimer' })).toBeTruthy()
    expect(screen.getByText(/you decide and trade at your own broker/)).toBeTruthy()
  })
})
