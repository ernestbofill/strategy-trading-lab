import { UnauthorizedError } from '../errors'
import { sessionEnded } from '../session'
import type { Api } from '../types'
import {
  mockChangePassword,
  mockForgotPassword,
  mockGetAccount,
  mockLogIn,
  mockLogOut,
  mockResendVerification,
  mockResetPassword,
  mockSignUp,
  mockVerifyEmail,
  requireMockUser,
} from './account'
import { templateDetails, templates } from './data'
import { mockAdminLogIn, mockAdminLogOut, mockAdminSettings, mockResetAdminSetting, mockSendTestEmail, mockSetAdminSetting } from './admin'
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

/** Like a screen's data from the API: without a session the app hears that it ended, as on a 401. */
const signedIn = <T>(produce: () => T) =>
  later(() => {
    try {
      requireMockUser()
    } catch (error) {
      if (error instanceof UnauthorizedError) sessionEnded()
      throw error
    }
    return produce()
  })

export const mockApi: Api = {
  getTemplates: () => respond(templates),
  getTemplate: (key) => respond(templateDetails.find((t) => t.key === key)),
  getAccount: () => later(mockGetAccount),
  signUp: (signUp) => later(() => mockSignUp(signUp)),
  logIn: (email, password) => later(() => mockLogIn(email, password)),
  logOut: () => later(mockLogOut),
  verifyEmail: (token) => later(() => mockVerifyEmail(token)),
  resendVerification: () => later(mockResendVerification),
  forgotPassword: (email) => later(() => mockForgotPassword(email)),
  resetPassword: (token, password) => later(() => mockResetPassword(token, password)),
  changePassword: (currentPassword, newPassword) => later(() => mockChangePassword(currentPassword, newPassword)),
  getDashboard: () => signedIn(dashboard),
  startStrategy: (strategy) => signedIn(() => startMockStrategy(strategy)),
  adminLogIn: (password) => later(() => mockAdminLogIn(password)),
  adminLogOut: () => later(mockAdminLogOut),
  getAdminSettings: () => later(mockAdminSettings),
  setAdminSetting: (key, value, shown) => later(() => mockSetAdminSetting(key, value, shown)),
  resetAdminSetting: (key, clear) => later(() => mockResetAdminSetting(key, clear)),
  sendAdminTestEmail: () => later(mockSendTestEmail),
}
