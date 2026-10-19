import type { Api } from './types'

// The .NET API. nginx serves it at /api on the app's own host; in development Vite proxies /api to it.
const BASE_URL = '/api'

/** GETs `path` as JSON; a 404 is `undefined` (not found), any other failure throws. */
async function get<T>(path: string): Promise<T | undefined> {
  const response = await fetch(`${BASE_URL}${path}`, { headers: { Accept: 'application/json' } })
  if (response.status === 404) return undefined
  if (!response.ok) throw new Error(`GET ${path} failed: HTTP ${response.status}`)
  return (await response.json()) as T
}

async function getRequired<T>(path: string): Promise<T> {
  const value = await get<T>(path)
  if (value === undefined) throw new Error(`GET ${path} failed: HTTP 404`)
  return value
}

export const httpApi: Api = {
  getTemplates: () => getRequired('/templates'),
  getTemplate: (key) => get(`/templates/${encodeURIComponent(key)}`),
}
