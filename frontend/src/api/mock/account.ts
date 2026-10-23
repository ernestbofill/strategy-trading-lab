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

/** Keyed by the email in lower case (the API compares without case), each holding the email as typed. */
const users = new Map<string, MockUser>([[demo.email, demo]])
/** Link token → what it does, by user key. Single use. */
const links = new Map<string, { user: string; purpose: 'verify' | 'reset' }>()
/** The log-in lock, as in Auth/LogInThrottle.cs: 5 tries in 15 minutes, then 15 minutes' wait. */
const tries = new Map<string, { count: number; since: number }>()
const WINDOW_MS = 15 * 60 * 1000
let current: string | null = demo.email

const key = (email: string) => email.trim().toLowerCase()

function sendLink(user: MockUser, purpose: 'verify' | 'reset') {
  const token = crypto.randomUUID()
  const email = user.email
  links.set(token, { user: key(email), purpose })
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

const isTimeZone = (zone: string) => {
  try {
    return Boolean(zone) && Boolean(new Intl.DateTimeFormat('en', { timeZone: zone }))
  } catch {
    return false
  }
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
  if (s.locale !== 'en' && s.locale !== 'ca') fields.locale = 'Pick English or Catalan.'
  if (!isTimeZone(s.timeZone)) fields.timeZone = 'Pick your time zone.'
  if (!s.disclaimerAccepted) fields.disclaimerAccepted = 'Tick the box to go on.'
  if (Object.keys(fields).length > 0) throw new InvalidFieldsError(fields)

  const k = key(s.email)
  const registered = users.get(k)
  if (registered) return console.info(`[mock email to ${registered.email}] Someone tried to sign up with your email.`)
  const user: MockUser = { ...s, email: s.email.trim(), emailVerified: false }
  users.set(k, user)
  current = k
  sendLink(user, 'verify')
}

export function mockLogIn(email: string, password: string) {
  const k = key(email)
  const now = Date.now()
  const counted = tries.get(k)
  const entry = counted && now - counted.since < WINDOW_MS ? counted : { count: 0, since: now }
  if (entry.count >= 5) throw new TooManyTriesError()
  // Counted before the password is checked; reaching 5 locks for a fresh 15 minutes.
  tries.set(k, entry.count + 1 >= 5 ? { count: 5, since: now } : { ...entry, count: entry.count + 1 })
  const user = users.get(k)
  if (!user || user.password !== password) throw new UnauthorizedError()
  tries.delete(k)
  current = k
}

export function mockLogOut() {
  current = null
}

/** The user of an unused link of that purpose. */
function findLink(token: string, purpose: 'verify' | 'reset'): MockUser {
  const link = links.get(token)
  const user = link?.purpose === purpose ? users.get(link.user) : undefined
  if (!user) throw new ExpiredLinkError()
  return user
}

/** Uses up the user's unused reset links, once the password has changed. */
function burnResetLinks(user: MockUser) {
  for (const [token, link] of links) if (link.purpose === 'reset' && link.user === key(user.email)) links.delete(token)
}

export function mockVerifyEmail(token: string) {
  findLink(token, 'verify').emailVerified = true
  links.delete(token)
}

export function mockResendVerification() {
  const user = currentUser()
  if (!user.emailVerified) sendLink(user, 'verify')
}

export function mockForgotPassword(email: string) {
  const user = users.get(key(email))
  if (user) sendLink(user, 'reset')
}

export function mockResetPassword(token: string, password: string) {
  const user = findLink(token, 'reset')
  const problem = passwordError(password, user.email)
  if (problem) throw new InvalidFieldsError({ password: problem }) // Checked before the link is used up, as in the API.
  burnResetLinks(user)
  user.password = password
  user.emailVerified = true
  tries.delete(key(user.email))
  current = key(user.email)
}

export function mockChangePassword(currentPassword: string, newPassword: string) {
  const user = currentUser()
  if (user.password !== currentPassword) throw new InvalidFieldsError({ currentPassword: "That isn't your current password." })
  const problem = passwordError(newPassword, user.email)
  if (problem) throw new InvalidFieldsError({ newPassword: problem })
  user.password = newPassword
  burnResetLinks(user)
}

/** For a screen's data: like the API's 401 without a session. */
export const requireMockUser = () => void currentUser()
