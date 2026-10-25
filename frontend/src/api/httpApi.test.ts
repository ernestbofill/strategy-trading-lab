import { afterEach, describe, expect, it, vi } from 'vitest'
import { ExpiredLinkError, ForbiddenError, InvalidValueError, NameTakenError, TooManyTriesError, UnauthorizedError } from './errors'
import { onSessionEnded } from './session'
import { httpApi } from './httpApi'
import type { NewStrategy } from './types'

const respond = (status: number, body?: unknown) =>
  vi.stubGlobal('fetch', vi.fn(async () => new Response(body === undefined ? null : JSON.stringify(body), { status })))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('httpApi', () => {
  it('reads the templates from /api/templates', async () => {
    respond(200, [{ key: 'dip' }])
    await expect(httpApi.getTemplates()).resolves.toEqual([{ key: 'dip' }])
    expect(fetch).toHaveBeenCalledWith('/api/templates', expect.anything())
  })

  it('reads one template from /api/templates/{key}', async () => {
    respond(200, { key: 'bear' })
    await expect(httpApi.getTemplate('bear')).resolves.toEqual({ key: 'bear' })
    expect(fetch).toHaveBeenCalledWith('/api/templates/bear', expect.anything())
  })

  it('treats a 404 as not found', async () => {
    respond(404)
    await expect(httpApi.getTemplate('dip')).resolves.toBeUndefined()
  })

  it('fails on a server error', async () => {
    respond(503)
    await expect(httpApi.getTemplates()).rejects.toThrow('HTTP 503')
  })

  it('reads the dashboard from /api/dashboard', async () => {
    respond(200, { today: '2026-10-20', strategies: [], waiting: [] })
    await expect(httpApi.getDashboard()).resolves.toMatchObject({ today: '2026-10-20' })
    expect(fetch).toHaveBeenCalledWith('/api/dashboard', expect.anything())
  })
})

describe('httpApi.startStrategy', () => {
  const strategy: NewStrategy = {
    templateKey: 'dip',
    name: 'Mine',
    mode: 'live',
    currency: 'EUR',
    budget: { amount: 3000, step: 100, maxSteps: 5 },
    assetIds: ['a1'],
    settings: {},
  }

  it('posts the strategy as JSON to /api/strategies and returns the created one', async () => {
    respond(201, { id: 's1', name: 'Mine' })
    await expect(httpApi.startStrategy(strategy)).resolves.toEqual({ id: 's1', name: 'Mine' })
    const [path, init] = vi.mocked(fetch).mock.calls[0]
    expect(path).toBe('/api/strategies')
    expect(init).toMatchObject({ method: 'POST', headers: expect.objectContaining({ 'Content-Type': 'application/json' }) })
    expect(JSON.parse(init?.body as string)).toEqual(strategy)
  })

  it('rejects with NameTakenError when the name is taken', async () => {
    respond(409)
    await expect(httpApi.startStrategy(strategy)).rejects.toBeInstanceOf(NameTakenError)
  })

  it('fails on any other error', async () => {
    respond(400)
    await expect(httpApi.startStrategy(strategy)).rejects.toThrow('HTTP 400')
  })
})

describe('httpApi admin', () => {
  it('maps 401, 403 and 429 to their errors', async () => {
    respond(401)
    await expect(httpApi.getAdminSettings()).rejects.toBeInstanceOf(UnauthorizedError)
    respond(403)
    await expect(httpApi.getAdminSettings()).rejects.toBeInstanceOf(ForbiddenError)
    respond(429)
    await expect(httpApi.sendAdminTestEmail()).rejects.toBeInstanceOf(TooManyTriesError)
  })

  it("passes on the server's reason when a value is refused", async () => {
    respond(400, { errors: { value: ['Use a port from 1 to 65535.'] } })
    await expect(httpApi.setAdminSetting('SMTP_PORT', '0')).rejects.toThrow(new InvalidValueError('Use a port from 1 to 65535.'))
  })

  it("sends the shown values with a secret, and passes on a 409's reason", async () => {
    respond(409, 'The mail settings changed since this page loaded.')
    await expect(httpApi.setAdminSetting('SMTP_PASSWORD', 'x', { SMTP_HOST: 'smtp.gmail.com' })).rejects.toThrow(
      new InvalidValueError('The mail settings changed since this page loaded.'),
    )
    expect(JSON.parse(vi.mocked(fetch).mock.calls[0][1]?.body as string)).toEqual({ value: 'x', expected: { SMTP_HOST: 'smtp.gmail.com' } })
  })

  it('puts a value, and deletes to reset or, with clear, to clear', async () => {
    respond(204)
    await httpApi.setAdminSetting('SMTP_HOST', 'smtp.gmail.com')
    await httpApi.resetAdminSetting('SMTP_HOST')
    await httpApi.resetAdminSetting('SMTP_PASSWORD', true)
    const calls = vi.mocked(fetch).mock.calls
    expect(calls[0]).toEqual(['/api/admin/settings/SMTP_HOST', expect.objectContaining({ method: 'PUT', body: '{"value":"smtp.gmail.com"}' })])
    expect(calls[1]).toEqual(['/api/admin/settings/SMTP_HOST', expect.objectContaining({ method: 'DELETE' })])
    expect(calls[2][0]).toBe('/api/admin/settings/SMTP_PASSWORD?clear=true')
  })

  it('reads and updates admin assets', async () => {
    respond(200, [{ id: 'a1', symbol: 'ZS' }])
    await expect(httpApi.getAdminAssets()).resolves.toEqual([{ id: 'a1', symbol: 'ZS' }])
    expect(fetch).toHaveBeenCalledWith('/api/admin/assets', expect.anything())

    respond(204)
    await httpApi.setAdminAssetActive('a1', false)
    expect(fetch).toHaveBeenCalledWith('/api/admin/assets/a1/active', expect.objectContaining({ method: 'PUT', body: '{"active":false}' }))
  })
})

describe('httpApi admin test email', () => {
  it('posts to /api/admin/test-email and returns where it went', async () => {
    respond(200, { sentTo: 'me@gmail.com' })
    await expect(httpApi.sendAdminTestEmail()).resolves.toBe('me@gmail.com')
    expect(vi.mocked(fetch).mock.calls[0][0]).toBe('/api/admin/test-email')
  })

  it("passes on the server's reason when it can't send", async () => {
    respond(502, { detail: 'smtp.gmail.com:587 refused it: Username and Password not accepted' })
    await expect(httpApi.sendAdminTestEmail()).rejects.toThrow(
      new InvalidValueError('smtp.gmail.com:587 refused it: Username and Password not accepted'),
    )
  })

  it("treats nginx's 502 (the API down) as an error, not a refused value", async () => {
    respond(502)
    const error = await httpApi.sendAdminTestEmail().catch((e: unknown) => e)
    expect(error).toBeInstanceOf(Error)
    expect(error).not.toBeInstanceOf(InvalidValueError)
  })
})

describe('httpApi CSRF header', () => {
  it('sends X-Requested-With: stl on state-changing requests', async () => {
    respond(204)
    await httpApi.setAdminSetting('SMTP_HOST', 'smtp.gmail.com')
    respond(201, { id: 's1' })
    await httpApi.startStrategy({ templateKey: 'dip', name: 'x', mode: 'live', currency: 'EUR', budget: { amount: 3000, step: 100, maxSteps: 5 }, assetIds: [], settings: {} })
    for (const [, init] of vi.mocked(fetch).mock.calls) expect(init?.headers).toMatchObject({ 'X-Requested-With': 'stl' })
  })
})

describe('httpApi accounts', () => {
  it('reads the account, and null without a session', async () => {
    respond(200, { email: 'a@b.c' })
    await expect(httpApi.getAccount()).resolves.toEqual({ email: 'a@b.c' })
    expect(fetch).toHaveBeenCalledWith('/api/account/me', expect.anything())
    respond(401)
    await expect(httpApi.getAccount()).resolves.toBeNull()
  })

  it('posts with the app header and maps the refusals', async () => {
    respond(202, { message: 'Check your email.' })
    await httpApi.forgotPassword('a@b.c')
    const [url, init] = vi.mocked(fetch).mock.calls[0]
    expect(url).toBe('/api/account/forgot-password')
    expect(init).toMatchObject({ method: 'POST', headers: expect.objectContaining({ 'X-Requested-With': 'stl' }) })

    respond(401)
    await expect(httpApi.logIn('a@b.c', 'wrong')).rejects.toBeInstanceOf(UnauthorizedError)
    respond(429)
    await expect(httpApi.logIn('a@b.c', 'wrong')).rejects.toBeInstanceOf(TooManyTriesError)
    respond(400, { errors: { password: ['Use at least 10 characters.'] } })
    await expect(httpApi.resetPassword('t', 'short')).rejects.toMatchObject({
      fields: { password: 'Use at least 10 characters.' },
    })
    respond(400, { detail: 'This link has expired. Ask for a new one.' })
    await expect(httpApi.verifyEmail('t')).rejects.toBeInstanceOf(ExpiredLinkError)
  })

  it('tells the app when a screen finds the session gone', async () => {
    const ended = vi.fn()
    const stop = onSessionEnded(ended)
    respond(401)
    await expect(httpApi.getDashboard()).rejects.toBeInstanceOf(UnauthorizedError)
    stop()
    expect(ended).toHaveBeenCalledOnce()
  })

  it('ends the session on a 401 from a call that needs one, but not on a wrong log-in', async () => {
    const ended = vi.fn()
    const stop = onSessionEnded(ended)
    respond(401)
    await expect(httpApi.logIn('a@b.c', 'wrong')).rejects.toBeInstanceOf(UnauthorizedError)
    expect(ended).not.toHaveBeenCalled()
    respond(401)
    await expect(httpApi.changePassword('a', 'b')).rejects.toBeInstanceOf(UnauthorizedError)
    stop()
    expect(ended).toHaveBeenCalledOnce()
  })

  it('reads a plain 400 as an expired link only on the link endpoints', async () => {
    respond(400, { detail: 'Bad request' })
    await expect(httpApi.forgotPassword('a@b.c')).rejects.not.toBeInstanceOf(ExpiredLinkError)
  })
})

describe('httpApi database', () => {
  it('reads the version from /api/version', async () => {
    respond(200, { version: '0.6.0-alpha', commit: null, builtAt: null, database: 'dev' })
    await expect(httpApi.getVersion()).resolves.toMatchObject({ database: 'dev' })
    expect(fetch).toHaveBeenCalledWith('/api/version', expect.anything())
  })

  it('switches with a post to /api/admin/database/active', async () => {
    respond(204)
    await httpApi.switchAdminDatabase('production')
    const calls = vi.mocked(fetch).mock.calls.map(([path, init]) => [init?.method, path, init?.body])
    expect(calls).toEqual([['POST', '/api/admin/database/active', JSON.stringify({ name: 'production' })]])
  })

  it("passes on the server's reason for a refused change", async () => {
    respond(400, { detail: 'The production database has no URL: set DATABASE_URL_PRODUCTION on the server.' })
    await expect(httpApi.switchAdminDatabase('production')).rejects.toThrow(
      new InvalidValueError('The production database has no URL: set DATABASE_URL_PRODUCTION on the server.'),
    )
  })
})


describe('httpApi feedback', () => {
  it('submits, lists, updates and deletes feedback items via /api/feedback', async () => {
    const created = {
      id: 'f1',
      userEmail: 'reader@example.com',
      kind: 'bug',
      status: 'open',
      text: 'Typo here',
      route: '/dashboard',
      element: 'main > p',
      selectedText: 'strtegy',
      closedAt: null,
      createdAt: '2026-10-25T10:00:00Z',
      updatedAt: '2026-10-25T10:00:00Z',
    }
    respond(201, created)
    await expect(
      httpApi.submitFeedback({
        kind: 'bug',
        text: 'Typo here',
        route: '/dashboard',
        element: 'main > p',
        selectedText: 'strtegy',
      }),
    ).resolves.toEqual(created)
    expect(fetch).toHaveBeenCalledWith(
      '/api/feedback',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ 'X-Requested-With': 'stl' }),
      }),
    )

    respond(200, [created])
    await expect(httpApi.getFeedback('open')).resolves.toEqual([created])
    expect(fetch).toHaveBeenCalledWith('/api/feedback?status=open', expect.objectContaining({ method: 'GET' }))

    respond(200, { ...created, status: 'closed', closedAt: '2026-10-25T11:00:00Z' })
    await expect(httpApi.updateFeedback('f1', { status: 'closed' })).resolves.toMatchObject({ status: 'closed' })
    expect(fetch).toHaveBeenCalledWith(
      '/api/feedback/f1',
      expect.objectContaining({
        method: 'PATCH',
        body: JSON.stringify({ status: 'closed' }),
      }),
    )

    respond(204)
    await expect(httpApi.deleteFeedback('f1')).resolves.toBeUndefined()
    expect(fetch).toHaveBeenCalledWith('/api/feedback/f1', expect.objectContaining({ method: 'DELETE' }))
  })

  it('maps 403 and 429 refusals on feedback endpoints', async () => {
    respond(403)
    await expect(httpApi.submitFeedback({ kind: 'note', text: 'Unconfirmed email' })).rejects.toBeInstanceOf(
      ForbiddenError,
    )

    respond(429)
    await expect(httpApi.submitFeedback({ kind: 'note', text: 'Rate limited' })).rejects.toBeInstanceOf(
      TooManyTriesError,
    )

    respond(400, { errors: { text: ['Use at most 4,000 characters.'] } })
    await expect(httpApi.submitFeedback({ kind: 'note', text: 'Too long' })).rejects.toThrow(
      new InvalidValueError('Use at most 4,000 characters.'),
    )
  })
})
