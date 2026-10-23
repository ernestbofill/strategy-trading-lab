import { ExpiredLinkError, InvalidFieldsError, TooManyTriesError, UnauthorizedError } from '../errors'
import type { Account, SignUp } from '../types'

// MOCK: accounts as backend/AUTH.md describes them, in memory. It starts logged in as a demo user (password
// "a long password"), so the rest of the mock app works at once; log out to try the account screens.
// The emailed links are logged to the console instead of sent.

interface MockUser extends Account {
  password: string
}

const demo: MockUser = {
  email: 'demo@example.com',
  password: 'a long password',
  emailVerified: true,
  locale: 'en',
  timeZone: 'Europe/Madrid',
  overviewEmail: true,
}

const users = new Map<string, MockUser>([[demo.email, demo]])
/** Link token → what it does. Single use. */
const links = new Map<string, { email: string; purpose: 'verify' | 'reset' }>()
const failures = new Map<string, number>()
let current: string | null = demo.email

const key = (email: string) => email.trim().toLowerCase()

function sendLink(email: string, purpose: 'verify' | 'reset') {
  const token = crypto.randomUUID()
  links.set(token, { email, purpose })
  const path = purpose === 'verify' ? 'verify-email' : 'reset-password'
  console.info(`[mock email to ${email}] ${globalThis.location?.origin ?? ''}/${path}?token=${token}`)
}

/** The API's rules (Auth/AccountRules.cs): 10 to 128 characters, not the email. */
export function passwordError(password: string, email: string): string | null {
  if (password.length < 10) return 'Use at least 10 characters.'
  if (password.length > 128) return 'Use at most 128 characters.'
  if (key(password) === key(email)) return "Don't use your email as the password."
  return null
}

const isEmail = (v: string) => v.length <= 254 && /^[^@\s,;<>"()]+@[^@\s,;<>"()]+\.[^@\s,;<>"()]+$/.test(v.trim())

const currentUser = (): MockUser => {
  const user = current ? users.get(current) : undefined
  if (!user) throw new UnauthorizedError()
  return user
}

const publicAccount = ({ email, emailVerified, locale, timeZone, overviewEmail }: MockUser): Account => ({
  email,
  emailVerified,
  locale,
  timeZone,
  overviewEmail,
})

export const mockGetAccount = (): Account | null => (current && users.has(current) ? publicAccount(currentUser()) : null)

export function mockSignUp(s: SignUp) {
  const fields: Record<string, string> = {}
  if (!isEmail(s.email)) fields.email = 'Use an email address, e.g. you@gmail.com.'
  const passwordProblem = passwordError(s.password, s.email)
  if (passwordProblem) fields.password = passwordProblem
  if (!s.disclaimerAccepted) fields.disclaimerAccepted = 'Tick the box to go on.'
  if (Object.keys(fields).length > 0) throw new InvalidFieldsError(fields)

  const email = key(s.email)
  if (users.has(email)) return console.info(`[mock email to ${email}] Someone tried to sign up with your email.`)
  users.set(email, { ...s, email, emailVerified: false })
  current = email
  sendLink(email, 'verify')
}

export function mockLogIn(email: string, password: string) {
  const k = key(email)
  if ((failures.get(k) ?? 0) >= 5) throw new TooManyTriesError()
  const user = users.get(k)
  if (!user || user.password !== password) {
    failures.set(k, (failures.get(k) ?? 0) + 1)
    throw new UnauthorizedError()
  }
  failures.delete(k)
  current = k
}

export function mockLogOut() {
  current = null
}

function claimLink(token: string, purpose: 'verify' | 'reset'): MockUser {
  const link = links.get(token)
  const user = link?.purpose === purpose ? users.get(link.email) : undefined
  if (!user) throw new ExpiredLinkError()
  links.delete(token)
  return user
}

export function mockVerifyEmail(token: string) {
  claimLink(token, 'verify').emailVerified = true
}

export function mockResendVerification() {
  const user = currentUser()
  if (!user.emailVerified) sendLink(user.email, 'verify')
}

export function mockForgotPassword(email: string) {
  if (users.has(key(email))) sendLink(key(email), 'reset')
}

export function mockResetPassword(token: string, password: string) {
  const link = links.get(token)
  const user = link?.purpose === 'reset' ? users.get(link.email) : undefined
  if (!user) throw new ExpiredLinkError()
  const problem = passwordError(password, user.email)
  if (problem) throw new InvalidFieldsError({ password: problem }) // Checked before the link is used up, as in the API.
  claimLink(token, 'reset')
  user.password = password
  user.emailVerified = true
  failures.delete(user.email)
  current = user.email
}

export function mockChangePassword(currentPassword: string, newPassword: string) {
  const user = currentUser()
  if (user.password !== currentPassword) throw new InvalidFieldsError({ currentPassword: "That isn't your current password." })
  const problem = passwordError(newPassword, user.email)
  if (problem) throw new InvalidFieldsError({ newPassword: problem })
  user.password = newPassword
}

/** For a screen's data: like the API's 401 without a session. */
export const requireMockUser = () => void currentUser()
