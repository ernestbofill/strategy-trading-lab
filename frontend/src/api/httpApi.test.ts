import { afterEach, describe, expect, it, vi } from 'vitest'
import { httpApi } from './httpApi'

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
})
