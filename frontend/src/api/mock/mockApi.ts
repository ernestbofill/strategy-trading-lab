import type { Api } from '../types'
import { templateDetails, templates } from './data'
import {
  mockActiveDatabase,
  mockAdminDatabases,
  mockAdminLogIn,
  mockAdminLogOut,
  mockAdminSettings,
  mockResetAdminSetting,
  mockResetDatabaseUrl,
  mockSendTestEmail,
  mockSetAdminSetting,
  mockSetDatabaseUrl,
  mockSwitchDatabase,
} from './admin'
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
  adminLogIn: (password) => later(() => mockAdminLogIn(password)),
  adminLogOut: () => later(mockAdminLogOut),
  getAdminSettings: () => later(mockAdminSettings),
  setAdminSetting: (key, value, shown) => later(() => mockSetAdminSetting(key, value, shown)),
  resetAdminSetting: (key, clear) => later(() => mockResetAdminSetting(key, clear)),
  sendAdminTestEmail: () => later(mockSendTestEmail),
  getVersion: () => later(() => ({ version: 'mock', commit: null, builtAt: null, database: mockActiveDatabase() })),
  getAdminDatabases: () => later(mockAdminDatabases),
  switchAdminDatabase: (name) => later(() => mockSwitchDatabase(name)),
  setAdminDatabaseUrl: (name, url) => later(() => mockSetDatabaseUrl(name, url)),
  resetAdminDatabaseUrl: (name) => later(() => mockResetDatabaseUrl(name)),
}
