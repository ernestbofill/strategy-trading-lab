import { NameTakenError } from './errors'
import type { Api, StrategySummary } from './types'

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

/** POSTs `body` as JSON. A 409 means the name is taken; any other failure throws. */
async function post<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (response.status === 409) throw new NameTakenError()
  if (!response.ok) throw new Error(`POST ${path} failed: HTTP ${response.status}`)
  return (await response.json()) as T
}

export const httpApi: Api = {
  getTemplates: () => getRequired('/templates'),
  getTemplate: (key) => get(`/templates/${encodeURIComponent(key)}`),
  getDashboard: () => getRequired('/dashboard'),
  startStrategy: (strategy) => post<StrategySummary>('/strategies', strategy),
}
