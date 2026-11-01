// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { useEffect } from 'react'
import { MemoryRouter, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type Dashboard, type StrategySummary, type TemplateDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { AccountProvider } from '../account/AccountContext'
import { Confirm } from './Confirm'
import { NewStrategyDraftProvider, useNewStrategyDraft } from './NewStrategyDraft'

// Whether the app offers push on this device (the server's key and the browser).
const push = vi.hoisted(() => ({ offered: false }))
vi.mock('../account/usePushOffered', () => ({ usePushOffered: () => push.offered }))

afterEach(() => {
  push.offered = false
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const ai = {
  key: 'ai',
  name: "Your AI's top picks",
  signals: 'buys',
  watches: { kind: 'stocks', starterList: [{ id: 'id-ZS', symbol: 'ZS', name: 'Zscaler', what: '' }] },
  defaults: { version: 1, budget: { amount: 3000, step: 100, maxSteps: 5 }, params: {} },
  settings: [],
  aiRound: { everyDays: 14, topN: 1 },
} as unknown as TemplateDetail

/** Sets the draft to the AI template, as the earlier screens do, with push ticked on Choose the mode where it is offered. */
function ChooseAi() {
  const { switchTemplate, update } = useNewStrategyDraft()
  useEffect(() => {
    switchTemplate('ai')
    update({ push: push.offered })
  }, [switchTemplate, update])
  return null
}

function DashboardStub() {
  const navigate = useNavigate()
  const askPush = (useLocation().state as { askPush?: boolean } | null)?.askPush
  return (
    <>
      <button onClick={() => navigate(-1)}>Dashboard back</button>
      {askPush && <p>Asks to allow notifications</p>}
    </>
  )
}

function open() {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getAccount').mockResolvedValue({
    email: 'a@example.com', firstName: null, lastName: null, emailVerified: true, locale: 'en', timeZone: 'UTC', overviewEmail: true, alertEmails: true, currency: 'EUR', role: 'user', aiSharing: false,
  })
  vi.spyOn(api, 'getTemplate').mockResolvedValue(ai)
  vi.spyOn(api, 'getDashboard').mockResolvedValue({ strategies: [], waiting: [] } as unknown as Dashboard)
  render(
    <AccountProvider>
      <NewStrategyDraftProvider owner="a@example.com">
        <ChooseAi />
        <MemoryRouter initialEntries={['/new/budget', '/new/confirm']} initialIndex={1}>
          <Routes>
            <Route path="/new/budget" element={<p>Set your budget</p>} />
            <Route path="/new/confirm" element={<Confirm />} />
            <Route path="/" element={<DashboardStub />} />
          </Routes>
        </MemoryRouter>
      </NewStrategyDraftProvider>
    </AccountProvider>,
  )
}

describe('Confirm', () => {
  it('opens the dashboard for an AI strategy, and going back never reopens Confirm', async () => {
    open()
    const start = vi.spyOn(api, 'startStrategy').mockResolvedValue({ id: 'st' } as StrategySummary)

    fireEvent.click(await screen.findByRole('button', { name: 'Start the simulation' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Dashboard back' }))

    // Confirm would show the reset draft (Winners at a discount), one tap from a second strategy.
    expect(await screen.findByText('Set your budget')).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Ready to start' })).toBeNull()
    expect(start).toHaveBeenCalledTimes(1)
    expect(start.mock.calls[0][0]).toMatchObject({ templateKey: 'ai' })
  })

  it('starts with push and asks to allow notifications on the dashboard where push is offered', async () => {
    push.offered = true
    open()
    const start = vi.spyOn(api, 'startStrategy').mockResolvedValue({ id: 'st' } as StrategySummary)

    expect(await screen.findByText('App and email')).toBeTruthy()
    fireEvent.click(await screen.findByRole('button', { name: 'Start the simulation' }))

    expect(await screen.findByText('Asks to allow notifications')).toBeTruthy()
    expect(start.mock.calls[0][0]).toMatchObject({ push: true })
  })

  it('starts without push, and asks nothing, where push is not offered', async () => {
    open()
    const start = vi.spyOn(api, 'startStrategy').mockResolvedValue({ id: 'st' } as StrategySummary)

    expect(await screen.findByText('Email')).toBeTruthy()
    fireEvent.click(await screen.findByRole('button', { name: 'Start the simulation' }))

    expect(await screen.findByRole('button', { name: 'Dashboard back' })).toBeTruthy()
    expect(screen.queryByText('Asks to allow notifications')).toBeNull()
    expect(start.mock.calls[0][0]).toMatchObject({ push: false })
  })

  it('renders "Create an account to start it" for guests and links to sign-up with next', async () => {
    vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
    vi.spyOn(api, 'getAccount').mockResolvedValue(null)
    vi.spyOn(api, 'getTemplate').mockResolvedValue(ai)
    render(
      <AccountProvider>
        <NewStrategyDraftProvider owner={null}>
          <ChooseAi />
          <MemoryRouter initialEntries={['/new/confirm']}>
            <Routes>
              <Route path="/new/confirm" element={<Confirm />} />
            </Routes>
          </MemoryRouter>
        </NewStrategyDraftProvider>
      </AccountProvider>,
    )

    const action = await screen.findByRole('link', { name: 'Create an account to start it' })
    expect(action.getAttribute('href')).toBe('/sign-up?next=%2Fnew%2Fconfirm')
    // The overview email is an account setting: a guest gets no Edit link to it.
    expect(screen.queryByRole('link', { name: /Edit.*overview/i })).toBeNull()
  })

  it('disables starting when the user already runs 10 strategies', async () => {
    vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
    vi.spyOn(api, 'getAccount').mockResolvedValue({
      email: 'a@example.com', firstName: null, lastName: null, emailVerified: true, locale: 'en', timeZone: 'UTC', overviewEmail: true, alertEmails: true, currency: 'EUR', role: 'user', aiSharing: false,
    })
    vi.spyOn(api, 'getTemplate').mockResolvedValue(ai)
    const running = Array.from({ length: 10 }, (_, i) => ({ id: `s${i}`, name: `S${i}`, status: 'running' as const } as unknown as StrategySummary))
    vi.spyOn(api, 'getDashboard').mockResolvedValue({ strategies: running, waiting: [] } as unknown as Dashboard)
    render(
      <AccountProvider>
        <NewStrategyDraftProvider owner="a@example.com">
          <ChooseAi />
          <MemoryRouter initialEntries={['/new/confirm']}>
            <Routes>
              <Route path="/new/confirm" element={<Confirm />} />
            </Routes>
          </MemoryRouter>
        </NewStrategyDraftProvider>
      </AccountProvider>,
    )

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toMatch(/You run 10 strategies, the most for now/)
    expect(screen.queryByRole('button', { name: 'Start the simulation' })).toBeNull()
    expect(screen.getByText('Start the simulation').getAttribute('aria-disabled')).toBe('true')
  })
})
