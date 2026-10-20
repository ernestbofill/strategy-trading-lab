import type { Api } from '../types'
import { templateDetails, templates } from './data'
import { dashboard, startMockStrategy } from './dashboard'

// In-browser stand-in for the .NET backend. Adds a small delay so loading states are exercised.
const LATENCY_MS = 250

/** Runs `produce` after the delay; if it throws, the promise rejects. */
function later<T>(produce: () => T): Promise<T> {
  return new Promise((resolve, reject) =>
    setTimeout(() => {
      try {
        resolve(structuredClone(produce()))
      } catch (error) {
        reject(error)
      }
    }, LATENCY_MS),
  )
}

const respond = <T>(value: T) => later(() => value)

export const mockApi: Api = {
  getTemplates: () => respond(templates),
  getTemplate: (key) => respond(templateDetails.find((t) => t.key === key)),
  getDashboard: () => later(dashboard),
  startStrategy: (strategy) => later(() => startMockStrategy(strategy)),
}
