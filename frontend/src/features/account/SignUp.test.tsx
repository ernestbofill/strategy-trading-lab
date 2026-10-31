// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, InvalidFieldsError, type SignUpOptions } from '../../api'
import { I18nProvider } from '../../i18n/I18n'
import { SignUp } from './SignUp'

vi.mock('./AccountContext', () => ({
  useAccount: () => ({ state: { status: 'ready', account: null }, refresh: vi.fn().mockResolvedValue(null) }),
  useIsOwner: () => false,
}))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

function open(options: SignUpOptions, url = '/sign-up') {
  vi.spyOn(api, 'getSignUpOptions').mockResolvedValue(options)
  render(
    <MemoryRouter initialEntries={[url]}>
      <I18nProvider>
        <SignUp />
      </I18nProvider>
    </MemoryRouter>,
  )
}

const invite = { code: 'ABCD-EFGH', firstName: 'Anna', locale: 'en' as const, invitedBy: 'Ernest' }

describe('SignUp', () => {
  it('greets the invitee from an invite link and fills in their first name', async () => {
    open({ inviteOnly: true, invite }, '/sign-up?invite=abcd-efgh')

    expect(await screen.findByRole('heading', { name: 'Welcome, Anna' })).toBeTruthy()
    expect(screen.getByText('Ernest invited you to Strategy Trading Lab.')).toBeTruthy()
    expect(screen.getByText('ABCD-EFGH')).toBeTruthy()
    expect((screen.getByLabelText('First name') as HTMLInputElement).value).toBe('Anna')
    expect(api.getSignUpOptions).toHaveBeenCalledWith('abcd-efgh')
  })

  it('asks for an invite code while sign-ups are invite only', async () => {
    open({ inviteOnly: true, invite: null })

    expect(await screen.findByLabelText('Invite code')).toBeTruthy()
    expect(screen.getByText(/Sign-ups are by invitation for now/)).toBeTruthy()
  })

  it('says when the link’s invite can no longer be used', async () => {
    open({ inviteOnly: false, invite: null }, '/sign-up?invite=ZZZZ-ZZZZ')

    expect(await screen.findByText(/This invite can't be used any more/)).toBeTruthy()
    expect((screen.getByLabelText('Invite code') as HTMLInputElement).value).toBe('') // Cleared: open sign-ups need none.
  })

  it('hides the code field while sign-ups are open, until asked for, and sends the names and the code', async () => {
    open({ inviteOnly: false, invite: null })
    const signUp = vi.spyOn(api, 'signUp').mockRejectedValue(new InvalidFieldsError({ password: 'Use at least 10 characters.' }))

    fireEvent.click(await screen.findByRole('button', { name: 'I have an invite code' }))
    fireEvent.change(screen.getByLabelText('Invite code'), { target: { value: 'abcd efgh' } })
    fireEvent.change(screen.getByLabelText('First name'), { target: { value: 'Joan' } })
    fireEvent.change(screen.getByLabelText('Last name'), { target: { value: 'Vila' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Not financial advice/ }))
    fireEvent.submit(document.getElementById('sign-up')!)

    expect(await screen.findByText('Use at least 10 characters.')).toBeTruthy()
    expect(signUp).toHaveBeenCalledWith(expect.objectContaining({ firstName: 'Joan', lastName: 'Vila', inviteCode: 'abcd efgh' }))
  })
})
