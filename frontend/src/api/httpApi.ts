import { ExpiredLinkError, InvalidFieldsError, InvalidValueError, NameTakenError, TooManyTriesError, UnauthorizedError } from './errors'
import { sessionEnded } from './session'
import type { Account, Api, StrategySummary } from './types'

// The .NET API. nginx serves it at /api on the app's own host; in development Vite proxies /api to it.
const BASE_URL = '/api'

/** Every state-changing request carries it; the API refuses those without it (CSRF, backend/AUTH.md). */
const APP_HEADER = { 'X-Requested-With': 'stl' }

/** A 401 on a screen's data means the session has ended: the app goes to Log-in. */
function endedSession(): never {
  sessionEnded()
  throw new UnauthorizedError()
}

/** GETs `path` as JSON; a 404 is `undefined` (not found), any other failure throws. */
async function get<T>(path: string): Promise<T | undefined> {
  const response = await fetch(`${BASE_URL}${path}`, { headers: { Accept: 'application/json' } })
  if (response.status === 404) return undefined
  if (response.status === 401) endedSession()
  if (!response.ok) throw new Error(`GET ${path} failed: HTTP ${response.status}`)
  return (await response.json()) as T
}

async function getRequired<T>(path: string): Promise<T> {
  const value = await get<T>(path)
  if (value === undefined) throw new Error(`GET ${path} failed: HTTP 404`)
  return value
}

/** POSTs `body` as JSON. A 409 means the name is taken; any other failure throws. */
async function post<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', ...APP_HEADER },
    body: JSON.stringify(body),
  })
  if (response.status === 409) throw new NameTakenError()
  if (response.status === 401) endedSession()
  if (!response.ok) throw new Error(`POST ${path} failed: HTTP ${response.status}`)
  return (await response.json()) as T
}

/** Sends a request to the admin API and maps its refusals to errors the page can show. */
async function admin(method: string, path: string, body?: unknown): Promise<Response> {
  const response = await fetch(`${BASE_URL}/admin${path}`, {
    method,
    headers: { Accept: 'application/json', ...APP_HEADER, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (response.status === 401) throw new UnauthorizedError()
  if (response.status === 429) throw new TooManyTriesError()
  if (response.status === 409) throw new InvalidValueError((await response.json().catch(() => null)) ?? 'Reload and try again.')
  if (response.status === 400 || response.status === 403 || response.status === 502) {
    const problem = (await response.json().catch(() => null)) as { errors?: Record<string, string[]>; detail?: string } | null
    const reason = problem?.errors?.value?.[0] ?? problem?.detail
    // A 502 without the API's reason is nginx: the API itself is down, not a refused value.
    if (reason) throw new InvalidValueError(reason)
    if (response.status !== 502) throw new InvalidValueError('That was refused.')
  }
  if (!response.ok) throw new Error(`${method} /admin${path} failed: HTTP ${response.status}`)
  return response
}

type Problem = { errors?: Record<string, string[]>; detail?: string } | null

interface AccountCall {
  /** A link endpoint (verify, reset): a 400 with only a message is an expired or used link. */
  link?: boolean
  /** Needs a session: a 401 means it ended, and the app goes to Log-in (on log-in itself, 401 is a wrong password). */
  session?: boolean
}

/** POSTs to the account API. A 400 with field errors is `InvalidFieldsError`; 401 is `UnauthorizedError`, 429 too many tries. */
async function account(path: string, body?: unknown, { link, session }: AccountCall = {}): Promise<Response> {
  const response = await fetch(`${BASE_URL}/account${path}`, {
    method: 'POST',
    headers: { Accept: 'application/json', ...APP_HEADER, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (response.status === 400) {
    const problem = (await response.json().catch(() => null)) as Problem
    if (problem?.errors) throw new InvalidFieldsError(Object.fromEntries(Object.entries(problem.errors).map(([k, v]) => [k, v[0] ?? ''])))
    if (link) throw new ExpiredLinkError()
  }
  if (response.status === 401) {
    if (session) endedSession()
    throw new UnauthorizedError()
  }
  if (response.status === 429) throw new TooManyTriesError()
  if (!response.ok) throw new Error(`POST /account${path} failed: HTTP ${response.status}`)
  return response
}

async function getAccount(): Promise<Account | null> {
  const response = await fetch(`${BASE_URL}/account/me`, { headers: { Accept: 'application/json' } })
  if (response.status === 401) return null
  if (!response.ok) throw new Error(`GET /account/me failed: HTTP ${response.status}`)
  return (await response.json()) as Account
}

const settingPath = (key: string) => `/settings/${encodeURIComponent(key)}`

export const httpApi: Api = {
  getAccount,
  signUp: async (signUp) => void (await account('/sign-up', signUp)),
  logIn: async (email, password) => void (await account('/log-in', { email, password })),
  logOut: async () => void (await account('/log-out')),
  verifyEmail: async (token) => void (await account('/verify-email', { token }, { link: true })),
  resendVerification: async () => void (await account('/resend-verification', undefined, { session: true })),
  forgotPassword: async (email) => void (await account('/forgot-password', { email })),
  resetPassword: async (token, password) => void (await account('/reset-password', { token, password }, { link: true })),
  changePassword: async (currentPassword, newPassword) =>
    void (await account('/change-password', { currentPassword, newPassword }, { session: true })),
  getTemplates: () => getRequired('/templates'),
  getTemplate: (key) => get(`/templates/${encodeURIComponent(key)}`),
  getDashboard: () => getRequired('/dashboard'),
  startStrategy: (strategy) => post<StrategySummary>('/strategies', strategy),
  adminLogIn: async (password) => void (await admin('POST', '/log-in', { password })),
  adminLogOut: async () => void (await admin('POST', '/log-out')),
  getAdminSettings: async () => (await admin('GET', '/settings')).json(),
  setAdminSetting: async (key, value, shown) => void (await admin('PUT', settingPath(key), { value, expected: shown })),
  sendAdminTestEmail: async () => ((await (await admin('POST', '/test-email')).json()) as { sentTo: string }).sentTo,
  resetAdminSetting: async (key, clear) => void (await admin('DELETE', `${settingPath(key)}${clear ? '?clear=true' : ''}`)),
  getVersion: () => getRequired('/version'),
  getAdminDatabases: async () => (await admin('GET', '/database')).json(),
  switchAdminDatabase: async (name) => void (await admin('POST', '/database/active', { name })),
}
