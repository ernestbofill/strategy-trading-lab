import { afterEach, describe, expect, it, vi } from 'vitest'
import { InvalidValueError, NameTakenError, TooManyTriesError, UnauthorizedError } from './errors'
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
    budget: { amount: 3000, step: 100, maxSteps: 5 },
    assetIds: ['a1'],
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
  it('logs in by posting the password to /api/admin/log-in', async () => {
    respond(204)
    await httpApi.adminLogIn('STL')
    const [path, init] = vi.mocked(fetch).mock.calls[0]
    expect(path).toBe('/api/admin/log-in')
    expect(init).toMatchObject({ method: 'POST', body: JSON.stringify({ password: 'STL' }) })
  })

  it('maps 401 and 429 to their errors', async () => {
    respond(401)
    await expect(httpApi.getAdminSettings()).rejects.toBeInstanceOf(UnauthorizedError)
    respond(429)
    await expect(httpApi.adminLogIn('x')).rejects.toBeInstanceOf(TooManyTriesError)
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
    await httpApi.adminLogOut()
    respond(201, { id: 's1' })
    await httpApi.startStrategy({ templateKey: 'dip', name: 'x', mode: 'live', budget: { amount: 3000, step: 100, maxSteps: 5 }, assetIds: [] })
    for (const [, init] of vi.mocked(fetch).mock.calls) expect(init?.headers).toMatchObject({ 'X-Requested-With': 'stl' })
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

  it("passes on the server's reason for a refused change, a 403 included", async () => {
    respond(403, { detail: 'Set ADMIN_PASSWORD on the server before changing the database here.' })
    await expect(httpApi.switchAdminDatabase('dev')).rejects.toThrow(new InvalidValueError('Set ADMIN_PASSWORD on the server before changing the database here.'))
  })
})
