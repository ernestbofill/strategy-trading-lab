import type { Api } from '../types'
import { templates } from './data'

// In-browser stand-in for the .NET backend. Adds a small delay so loading states are exercised.
const LATENCY_MS = 250

function respond<T>(value: T): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(structuredClone(value)), LATENCY_MS))
}

export const mockApi: Api = {
  getTemplates: () => respond(templates),
  getTemplate: (key) => respond(templates.find((t) => t.key === key)),
}
