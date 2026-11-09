// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { useEffect } from 'react'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type TemplateDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { AccountProvider } from '../account/AccountContext'
import { RequireAccount } from '../account/RequireAccount'
import { ChooseMode } from './ChooseMode'
import { NewStrategyDraftProvider, useNewStrategyDraft } from './NewStrategyDraft'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

/** Sets the draft to manual (the code's `live`), as "Follow it manually" or an earlier visit would. */
function StartLive() {
  const { update } = useNewStrategyDraft()
  useEffect(() => update({ mode: 'live' }), [update])
  return <Outlet />
}

function open(emailVerified: boolean) {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getAccount').mockResolvedValue({
    email: 'a@example.com', firstName: null, lastName: null, emailVerified, locale: 'en', timeZone: 'UTC', overviewEmail: true, overviewPush: true, alertEmails: true, alertPush: true, currency: 'EUR', role: 'user', aiSharing: false, notifyHour: 7,
  })
  vi.spyOn(api, 'getTemplate').mockResolvedValue({ key: 'dip', name: 'Winners at a discount', signals: 'buysOnly', aiRound: null, backtest: {} } as unknown as TemplateDetail)
  render(
    <AccountProvider>
      <NewStrategyDraftProvider owner="a@example.com">
        <MemoryRouter initialEntries={['/new/mode']}>
          <Routes>
            <Route element={<RequireAccount />}>
              <Route element={<StartLive />}>
                <Route path="/new/mode" element={<ChooseMode />} />
              </Route>
            </Route>
          </Routes>
        </MemoryRouter>
      </NewStrategyDraftProvider>
    </AccountProvider>,
  )
}

describe('ChooseMode', () => {
  it('opens for a guest, who may pick Manual', async () => {
    vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
    vi.spyOn(api, 'getAccount').mockResolvedValue(null)
    vi.spyOn(api, 'getTemplate').mockResolvedValue({ key: 'dip', name: 'Winners at a discount', backtest: {} } as unknown as TemplateDetail)
    render(
      <AccountProvider>
        <NewStrategyDraftProvider owner={null}>
          <MemoryRouter initialEntries={['/new/mode']}>
            <Routes>
              <Route path="/new/mode" element={<ChooseMode />} />
            </Routes>
          </MemoryRouter>
        </NewStrategyDraftProvider>
      </AccountProvider>,
    )

    const live = await screen.findByRole('radio', { name: /Manual/ })
    expect((live as HTMLInputElement).disabled).toBe(false)
  })

  it('keeps Manual off until the email is confirmed, and falls back to a simulation', async () => {
    open(false)

    const live = await screen.findByRole('radio', { name: /Manual/ })
    expect((live as HTMLInputElement).disabled).toBe(true)
    await waitFor(() => expect((screen.getByRole('radio', { name: /Simulation/ }) as HTMLInputElement).checked).toBe(true))
    expect(screen.getByText('Confirm your email to use Manual.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Send the link again' })).toBeTruthy()
  })

  it('lets a confirmed account pick Manual', async () => {
    open(true)

    const live = (await screen.findByRole('radio', { name: /Manual/ })) as HTMLInputElement
    expect(live.disabled).toBe(false)
    fireEvent.click(live)
    expect(live.checked).toBe(true)
    expect(await screen.findByText('Get an alert for each buy signal')).toBeTruthy()
    expect(screen.queryByText('Confirm your email to use Manual.')).toBeNull()
  })
})
