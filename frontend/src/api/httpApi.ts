import { currentLocale } from '../i18n/locale'
import { ExpiredLinkError, ForbiddenError, InvalidFieldsError, InvalidValueError, NameTakenError, TooManyTriesError, UnauthorizedError } from './errors'
import { sessionEnded } from './session'
import type { Account, Api, FeedbackItem, SignUpOptions, StrategySummary, StrategyTradeItem } from './types'

// The .NET API. nginx serves it at /api on the app's own host; in development Vite proxies /api to it.
const BASE_URL = '/api'

/** Every state-changing request carries it; the API refuses those without it (CSRF, backend/AUTH.md). */
const APP_HEADER = { 'X-Requested-With': 'stl' }

const langHeader = () => ({ 'Accept-Language': currentLocale() })

/** A 401 on a screen's data means the session has ended: the app goes to Log-in. */
function endedSession(): never {
  sessionEnded()
  throw new UnauthorizedError()
}

type Problem = { errors?: Record<string, string[]>; detail?: string } | null

/** A refusal's body: the API's problem, or its reason as a JSON string; null when it has none. */
const readBody = (response: Response) => response.json().catch(() => null) as Promise<Problem | string>

/** What a refusal says: the API's reason, else its first field error, else its detail. */
const reasonOf = (problem: Problem | string) =>
  typeof problem === 'string' ? problem : ((problem?.errors ? Object.values(problem.errors).flat()[0] : undefined) ?? problem?.detail)

/** A 400's field errors, under their fields: each field's first message, or all of them joined. */
const fieldErrors = (errors: Record<string, string[]>, joined = false) =>
  new InvalidFieldsError(Object.fromEntries(Object.entries(errors).map(([k, v]) => [k, joined ? v.join(' ') : (v[0] ?? '')])))

/** How an endpoint family's refusals read, besides what every call shares (401 ends the session, 403, 429). */
interface Refusals {
  /** Statuses that aren't failures: the caller reads them (a 404 that is fine, `/account/me`'s 401). */
  ok?: number[]
  /** A 401 is a refusal, not a session that ended (log-in: a wrong password). */
  noSession?: boolean
  /** A 409: what it means, from the API's reason. By default the reason itself, as `InvalidValueError` ("Reload and try again." without one). */
  conflict?: (reason: string | null) => Error
  /** A 400's field errors: each field's first message (the default), all of them joined, or left to `refused`. */
  fields?: 'first' | 'joined' | 'none'
  /** Any other refusal: what it means, from its body; undefined fails it as an HTTP error. */
  refused?: (problem: Problem | string, status: number) => Error | undefined
}

/**
 * Every call to the API: JSON in and out, the app header (the API refuses state changes without it: CSRF, backend/AUTH.md) and
 * the app's language. Resolves to the response when it is ok (or one of `ok`), else throws what the refusal means.
 */
async function request(method: string, path: string, body?: unknown, refusals: Refusals = {}): Promise<Response> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method,
    headers: { Accept: 'application/json', ...APP_HEADER, ...langHeader(), ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const { status } = response
  if (response.ok || refusals.ok?.includes(status)) return response
  if (status === 401) {
    if (refusals.noSession) throw new UnauthorizedError()
    endedSession()
  }
  if (status === 403) throw new ForbiddenError()
  if (status === 429) throw new TooManyTriesError()
  if (status === 409) {
    const reason = await readBody(response)
    throw (refusals.conflict ?? reasonOr('Reload and try again.'))(typeof reason === 'string' ? reason : null)
  }
  const problem = status === 400 || refusals.refused ? await readBody(response) : null
  const fields = refusals.fields ?? 'first'
  if (status === 400 && fields !== 'none' && typeof problem !== 'string' && problem?.errors) throw fieldErrors(problem.errors, fields === 'joined')
  const refused = refusals.refused?.(problem, status)
  if (refused) throw refused
  throw new Error(`${method} ${path} failed: HTTP ${status}`)
}

/** GETs `path` as JSON; a 404 is `undefined` (not found), any other failure throws. */
async function get<T>(path: string): Promise<T | undefined> {
  const response = await request('GET', path, undefined, { ok: [404] })
  return response.status === 404 ? undefined : ((await response.json()) as T)
}

async function getRequired<T>(path: string): Promise<T> {
  return (await (await request('GET', path)).json()) as T
}

const json = async <T,>(response: Promise<Response>) => (await (await response).json()) as T
const done = async (response: Promise<Response>) => void (await response)

/** A 409 with the API's reason: the value was refused as it stands (already answered, not due, ended). */
const reasonOr = (fallback: string) => (reason: string | null) => new InvalidValueError(reason ?? fallback)

/** A new strategy: a 409 means the name is taken. */
const post = <T,>(path: string, body: unknown) => json<T>(request('POST', path, body, { conflict: () => new NameTakenError() }))

const put = (path: string, body: unknown) => done(request('PUT', path, body))

const tradesRequest = (method: string, path: string, body?: unknown) =>
  request(method, path, body, { conflict: reasonOr('Could not perform trade action.') })

/** A strategy action (rename, archive). A 409 is `InvalidValueError` with the API's reason (a rename's: the name is taken). */
const strategyAction = (method: 'PUT' | 'POST', id: string, action: string, body?: unknown) =>
  done(request(method, `/strategies/${encodeURIComponent(id)}/${action}`, body, {
    conflict: action === 'name' ? () => new NameTakenError() : undefined,
  }))

/** POSTs an answer to a signal. A 409 (already answered) is `InvalidValueError`. */
const signalPost = (path: string, body?: unknown) =>
  request('POST', `/signals${path}`, body, { conflict: reasonOr('This signal is already answered.') })

/** Sends a request to the admin API and maps its refusals to errors the page can show. */
const admin = (method: string, path: string, body?: unknown) =>
  request(method, `/admin${path}`, body, {
    fields: 'none',
    refused: (problem, status) => {
      if (status !== 400 && status !== 502) return undefined
      const p = typeof problem === 'string' ? null : problem
      // A setting's one field is `value`; a form's (an asset's symbol, name and description) go by name.
      if (p?.errors && Object.keys(p.errors).some((k) => k !== 'value')) return fieldErrors(p.errors)
      const reason = p?.errors?.value?.[0] ?? p?.detail
      // A 502 without the API's reason is nginx: the API itself is down, not a refused value.
      if (reason) return new InvalidValueError(reason)
      return status === 502 ? undefined : new InvalidValueError('That was refused.')
    },
  })

interface AccountCall {
  /** A link endpoint (verify, reset): a 400 with only a message is an expired or used link. */
  link?: boolean
  /** Needs a session: a 401 means it ended, and the app goes to Log-in (on log-in itself, 401 is a wrong password). */
  session?: boolean
}

/** POSTs to the account API. A 400 with field errors is `InvalidFieldsError`; 401 is `UnauthorizedError`, 429 too many tries. */
const account = (path: string, body?: unknown, { link, session }: AccountCall = {}) =>
  done(request('POST', `/account${path}`, body, {
    noSession: !session,
    refused: (_, status) => (link && status === 400 ? new ExpiredLinkError() : undefined),
  }))

async function getSignUpOptions(invite?: string): Promise<SignUpOptions> {
  const query = invite ? `?invite=${encodeURIComponent(invite)}` : ''
  return json(request('GET', `/account/sign-up-options${query}`, undefined, { noSession: true }))
}

async function getAccount(): Promise<Account | null> {
  const response = await request('GET', '/account/me', undefined, { ok: [401] })
  return response.status === 401 ? null : ((await response.json()) as Account)
}

const settingPath = (key: string) => `/settings/${encodeURIComponent(key)}`

/** A request about AI rounds. A 400's field errors are joined, a 409 (not due, done already) is `InvalidValueError`. */
const aiRequest = (method: string, path: string, body?: unknown) =>
  request(method, path, body, { conflict: reasonOr('This AI round is already done.'), fields: 'joined' })

const aiPath = (strategyId: string, rest: string) => `/strategies/${encodeURIComponent(strategyId)}${rest}`

/** Sends a request to the feedback API: a refused comment says why. */
const feedback = (method: string, path: string, body?: unknown) =>
  request(method, `/feedback${path}`, body, {
    fields: 'none',
    refused: (problem, status) =>
      status === 400
        ? new InvalidValueError(reasonOf(problem) ?? 'That comment was refused.')
        : undefined,
  })

/** The stock and fund search. Any refusal (a short query, not a fund, the price source down) carries a message for the user. */
const assets = (path: string, body?: unknown) =>
  request(body === undefined ? 'GET' : 'POST', `/assets${path}`, body, {
    fields: 'none',
    refused: (problem) => {
      const message = reasonOf(problem)
      return message ? new InvalidValueError(message) : undefined
    },
  })

export const httpApi: Api = {
  getAccount,
  signUp: (signUp) => account('/sign-up', signUp),
  getSignUpOptions,
  setName: (firstName, lastName) => account('/name', { firstName, lastName }, { session: true }),
  logIn: (email, password) => account('/log-in', { email, password }),
  logOut: () => account('/log-out'),
  verifyEmail: (token) => account('/verify-email', { token }, { link: true }),
  resendVerification: () => account('/resend-verification', undefined, { session: true }),
  forgotPassword: (email) => account('/forgot-password', { email }),
  resetPassword: (token, password) => account('/reset-password', { token, password }, { link: true }),
  deleteAccount: (password) => account('/delete', { password }, { session: true }),
  changePassword: (currentPassword, newPassword) => account('/change-password', { currentPassword, newPassword }, { session: true }),
  setCurrency: (currency) => account('/currency', { currency }, { session: true }),
  setLocale: (locale) => account('/locale', { locale }, { session: true }),
  setTimeZone: (timeZone) => account('/time-zone', { timeZone }, { session: true }),
  setNotifyHour: (hour) => account('/notify-hour', { hour }, { session: true }),
  setOverviewEmail: (enabled) => account('/overview-email', { enabled }, { session: true }),
  setAlertEmails: (enabled) => account('/alert-emails', { enabled }, { session: true }),
  setAlertPush: (enabled) => account('/alert-push', { enabled }, { session: true }),
  unsubscribe: (token, kind) =>
    done(request('POST', `/email/unsubscribe/${encodeURIComponent(token)}/${kind}`, undefined, {
      noSession: true,
      refused: (_, status) => (status === 404 ? new ExpiredLinkError() : undefined),
    })),
  getTemplates: () => getRequired('/templates'),
  getTemplate: (key, version) => get(`/templates/${encodeURIComponent(key)}${version === undefined ? '' : `?version=${version}`}`),
  getDashboard: () => getRequired('/dashboard'),
  getSummary: () => getRequired('/summary'),
  getStrategy: (id) => get(`/strategies/${encodeURIComponent(id)}`),
  getStockChart: (id, assetId) => get(`/strategies/${encodeURIComponent(id)}/stocks/${encodeURIComponent(assetId)}/chart`),
  updateStrategySettings: (id, settings) => put(`/strategies/${encodeURIComponent(id)}/settings`, { settings }),
  updateStrategyAssets: (id, assetIds) => put(`/strategies/${encodeURIComponent(id)}/assets`, { assetIds }),
  getSignal: (id) => get(`/signals/${encodeURIComponent(id)}`),
  confirmSignal: (id, trade) => done(signalPost(`/${encodeURIComponent(id)}/trade`, trade)),
  logManualTrade: (strategyId, trade) => json<StrategyTradeItem>(tradesRequest('POST', `/strategies/${encodeURIComponent(strategyId)}/trades`, trade)),
  updateTrade: (tradeId, trade) => done(tradesRequest('PUT', `/trades/${encodeURIComponent(tradeId)}`, trade)),
  deleteTrade: (tradeId) => done(tradesRequest('DELETE', `/trades/${encodeURIComponent(tradeId)}`)),
  skipSignal: (id, reason) => done(signalPost(`/${encodeURIComponent(id)}/skip`, { reason })),
  snoozeSignal: async (id) => (await json<{ snoozedUntil: string }>(signalPost(`/${encodeURIComponent(id)}/snooze`))).snoozedUntil,
  searchAssets: (query, kind) =>
    json(assets(`/search?${new URLSearchParams({ q: query, kind })}`)),
  addAsset: (symbol, kind) => json(assets('', { symbol, kind })),
  startStrategy: (strategy) => post<StrategySummary>('/strategies', strategy),
  // Already gone (another tab, or a retry after a request that went through) is what the user asked for.
  deleteStrategy: (id) => done(request('DELETE', `/strategies/${encodeURIComponent(id)}`, undefined, { ok: [404] })),
  renameStrategy: (id, name) => strategyAction('PUT', id, 'name', { name }),
  getPushKey: async () => (await getRequired<{ publicKey: string | null }>('/push/key')).publicKey,
  savePushSubscription: (subscription) => put('/push/subscription', subscription),
  removePushSubscription: (endpoint) => done(request('POST', '/push/unsubscribe', { endpoint })),
  archiveStrategy: (id) => strategyAction('POST', id, 'archive'),
  getAdminSettings: () => json(admin('GET', '/settings')),
  getAdminInvites: () => json(admin('GET', '/invites')),
  createAdminInvite: (invite) => json(admin('POST', '/invites', invite)),
  sendAdminInvite: (id) => json(admin('POST', `/invites/${encodeURIComponent(id)}/send`)),
  revokeAdminInvite: (id) => done(admin('POST', `/invites/${encodeURIComponent(id)}/revoke`)),
  setAdminSetting: (key, value, shown) => done(admin('PUT', settingPath(key), { value, expected: shown })),
  sendAdminTestEmail: async () => (await json<{ sentTo: string }>(admin('POST', '/test-email'))).sentTo,
  resetAdminSetting: (key, clear) => done(admin('DELETE', `${settingPath(key)}${clear ? '?clear=true' : ''}`)),
  getVersion: () => getRequired('/version'),
  getAdminPrices: () => json(admin('GET', '/prices')),
  refreshAdminPrices: () => done(admin('POST', '/prices/refresh')),
  testAdminYahoo: () => json(admin('POST', '/prices/test')),
  getAdminDatabases: () => json(admin('GET', '/database')),
  switchAdminDatabase: (name) => done(admin('POST', '/database/active', { name })),
  getAdminAssets: () => json(admin('GET', '/assets')),
  getAdminUsers: () => json(admin('GET', '/users')),
  getAdminNotifications: () => json(admin('GET', '/notifications')),
  sendAdminTestPush: (push) => json(admin('POST', '/notifications/test', push)),
  getAdminAlerts: () => json(admin('GET', '/alerts')),
  dismissAdminAlert: (id) => done(admin('POST', `/alerts/${encodeURIComponent(id)}/dismiss`)),
  getAdminTemplates: () => json(admin('GET', '/templates')),
  saveAdminTemplateVersion: (key, change) => json(admin('POST', `/templates/${encodeURIComponent(key)}/versions`, change)),
  activateAdminTemplateVersion: (key, version) =>
    json(admin('PUT', `/templates/${encodeURIComponent(key)}/active`, { version })),
  setAdminAssetActive: (id, active) => done(admin('PUT', `/assets/${encodeURIComponent(id)}/active`, { active })),
  addAdminAsset: (symbol) => json(admin('POST', '/assets', { symbol })),
  updateAdminAsset: (id, edit) => done(admin('PUT', `/assets/${encodeURIComponent(id)}`, edit)),
  flushAdminPrices: () => json(admin('DELETE', '/assets/prices')),
  flushAdminAssetPrices: (id) => done(admin('DELETE', `/assets/${encodeURIComponent(id)}/prices`)),
  downloadAdminAssetPrices: (id, years) => json(admin('POST', `/assets/${encodeURIComponent(id)}/prices`, { years })),
  downloadAdminRates: (years) => json(admin('POST', '/assets/rates', { years })),
  submitFeedback: (item) => json<FeedbackItem>(feedback('POST', '', item)),
  getFeedback: (status) =>
    json<FeedbackItem[]>(feedback('GET', status ? `?status=${encodeURIComponent(status)}` : '')),
  updateFeedback: (id, patch) =>
    json<FeedbackItem>(feedback('PATCH', `/${encodeURIComponent(id)}`, patch)),
  deleteFeedback: (id) => done(feedback('DELETE', `/${encodeURIComponent(id)}`)),
  previewAiPrompt: (request) => json(aiRequest('POST', '/templates/ai/prompt', request)),
  getAiRound: (strategyId) => get(aiPath(strategyId, '/ai-round')),
  readAiAnswer: (strategyId, answer) => json(aiRequest('POST', aiPath(strategyId, '/ai-round/read'), { answer })),
  useAiScores: (strategyId, answer, aiModel, share, prompt, extra) =>
    json(aiRequest('POST', aiPath(strategyId, '/ai-round'), { answer, aiModel, share, prompt, extra })),
  skipAiRound: (strategyId, roundId) => done(aiRequest('POST', aiPath(strategyId, '/ai-round/skip'), { roundId })),
  getAiRounds: (strategyId) => get(aiPath(strategyId, '/ai-rounds')),
  updateAiPrompt: (strategyId, setup) => done(aiRequest('PUT', aiPath(strategyId, '/ai-prompt'), setup)),
  setAiSharing: (enabled) => account('/ai-sharing', { enabled }, { session: true }),
}
