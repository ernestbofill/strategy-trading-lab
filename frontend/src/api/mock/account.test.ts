import { afterEach, describe, expect, it, vi } from 'vitest'
import { ExpiredLinkError, InvalidFieldsError, TooManyTriesError, UnauthorizedError } from '../errors'
import { mockApi } from './mockApi'

const signUp = (email: string, password = 'a long password') =>
  mockApi.signUp({ email, password, locale: 'en', timeZone: 'Europe/Madrid', overviewEmail: true, disclaimerAccepted: true })

/** The token of the last link the mock "emailed" (logged). */
const lastLink = (info: ReturnType<typeof vi.spyOn>) => String(info.mock.calls.at(-1)?.[0]).split('token=')[1]

afterEach(() => vi.restoreAllMocks())

describe('mock accounts', () => {
  it('signs up, logs in at once, and verifies once by link', async () => {
    const info = vi.spyOn(console, 'info').mockImplementation(() => {})
    await signUp('new@example.com')
    expect(await mockApi.getAccount()).toMatchObject({ email: 'new@example.com', emailVerified: false })

    const token = lastLink(info)
    await mockApi.verifyEmail(token)
    expect(await mockApi.getAccount()).toMatchObject({ emailVerified: true })
    await expect(mockApi.verifyEmail(token)).rejects.toBeInstanceOf(ExpiredLinkError)
  })

  it('refuses the fields the API refuses', async () => {
    await expect(signUp('not an email', 'short')).rejects.toMatchObject({
      fields: { email: expect.any(String), password: 'Use at least 10 characters.' },
    })
    await expect(signUp('x@example.com', 'X@example.com')).rejects.toBeInstanceOf(InvalidFieldsError)
  })

  it('logs out, and a screen then finds no session', async () => {
    await mockApi.logOut()
    expect(await mockApi.getAccount()).toBeNull()
    await expect(mockApi.getDashboard()).rejects.toBeInstanceOf(UnauthorizedError)
  })

  it('locks an email after 5 wrong passwords, with or without an account', async () => {
    for (let i = 0; i < 5; i++) await expect(mockApi.logIn('nobody@example.com', 'wrong')).rejects.toBeInstanceOf(UnauthorizedError)
    await expect(mockApi.logIn('nobody@example.com', 'wrong')).rejects.toBeInstanceOf(TooManyTriesError)
  })

  it('resets the password by link and logs in with it', async () => {
    const info = vi.spyOn(console, 'info').mockImplementation(() => {})
    await mockApi.logOut()
    await mockApi.forgotPassword('demo@example.com')
    const token = lastLink(info)
    await expect(mockApi.resetPassword(token, 'short')).rejects.toBeInstanceOf(InvalidFieldsError)
    await mockApi.resetPassword(token, 'my new password')
    expect(await mockApi.getAccount()).toMatchObject({ email: 'demo@example.com' })
    await mockApi.logOut()
    await mockApi.logIn('demo@example.com', 'my new password')
    await expect(mockApi.changePassword('wrong', 'another password')).rejects.toMatchObject({
      fields: { currentPassword: "That isn't your current password." },
    })
  })

  it('keeps the email as typed, and changing the password uses up reset links', async () => {
    const info = vi.spyOn(console, 'info').mockImplementation(() => {})
    await signUp('Ann@Example.com')
    expect(await mockApi.getAccount()).toMatchObject({ email: 'Ann@Example.com' })
    await mockApi.forgotPassword('ann@example.com')
    const token = lastLink(info)
    await mockApi.changePassword('a long password', 'a brand new password')
    await expect(mockApi.resetPassword(token, 'yet another password')).rejects.toBeInstanceOf(ExpiredLinkError)
  })

  it('checks the language and the time zone', async () => {
    await expect(
      mockApi.signUp({ email: 'tz@example.com', password: 'a long password', locale: 'en', timeZone: 'Not/AZone', overviewEmail: true, disclaimerAccepted: true }),
    ).rejects.toMatchObject({ fields: { timeZone: 'Pick your time zone.' } })
  })
})
