// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type AdminInvite, type AdminSetting } from '../../api'
import { AdminInvites } from './AdminInvites'

vi.mock('../account/AccountContext', () => ({
  useAccount: () => ({ state: { status: 'ready', account: null } }),
  useIsOwner: () => true,
}))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const anna: AdminInvite = {
  id: '1', code: 'ABCD-EFGH', link: 'https://stl.test/sign-up?invite=ABCD-EFGH', message: 'Hi Anna! … ABCD-EFGH',
  firstName: 'Anna', lastName: 'Puig', email: null, locale: 'en', role: 'vip', maxUses: 1, uses: 0,
  createdAt: '2026-10-30T10:00:00Z', sentAt: null, revokedAt: null, joined: [],
}

const signUps = (value: string): AdminSetting => ({
  key: 'SIGN_UPS', group: 'Sign-ups', description: '', default: 'open', secret: false, value, isSet: true, source: 'page',
})

function open(invites: AdminInvite[] = [], gate = 'open') {
  vi.spyOn(api, 'getAdminInvites').mockResolvedValue(invites)
  vi.spyOn(api, 'getAdminSettings').mockResolvedValue([signUps(gate)])
  render(
    <MemoryRouter>
      <AdminInvites />
    </MemoryRouter>,
  )
}

describe('AdminInvites', () => {
  it('makes an invite without an email and shows its code, link and message to copy', async () => {
    open()
    const create = vi.spyOn(api, 'createAdminInvite').mockResolvedValue(anna)

    expect((screen.getByRole('button', { name: /Send by email/ }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(screen.getByLabelText('First name'), { target: { value: 'Anna' } })
    fireEvent.click(screen.getByRole('button', { name: 'Make it, copy the text' }))

    expect(await screen.findByRole('heading', { name: 'Invite for Anna Puig' })).toBeTruthy()
    expect(create).toHaveBeenCalledWith(expect.objectContaining({ firstName: 'Anna', locale: 'en', send: false }))
    expect(screen.getByText(anna.link)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Copy message' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'WhatsApp' }).getAttribute('href')).toBe(`https://wa.me/?text=${encodeURIComponent(anna.message)}`)
  })

  it('lists the invites with their state, and asks before revoking one', async () => {
    open([anna, { ...anna, id: '2', firstName: 'Joan', lastName: null, uses: 1, joined: [{ email: 'joan@example.com', firstName: 'Joan', lastName: 'Vila', joinedAt: '2026-10-30T12:00:00Z' }] }])
    const revoke = vi.spyOn(api, 'revokeAdminInvite').mockResolvedValue()

    expect(await screen.findByText('Not sent')).toBeTruthy()
    expect(screen.getByText('Joined')).toBeTruthy()
    expect(screen.getByText('joan@example.com')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Revoke' }))
    expect(revoke).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Sure? Revoke it' }))
    expect(revoke).toHaveBeenCalledWith('1')
  })

  it('switches sign-ups to invite only', async () => {
    open([], 'open')
    const set = vi.spyOn(api, 'setAdminSetting').mockResolvedValue()

    fireEvent.click(await screen.findByRole('radio', { name: 'Invite only' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Saved: only people with an invite can sign up.')).toBeTruthy()
    expect(set).toHaveBeenCalledWith('SIGN_UPS', 'invite_only')
  })
})
