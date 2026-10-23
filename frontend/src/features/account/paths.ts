import { dashboardPath } from '../new-strategy/paths'

export { accountPath } from '../dashboard/paths'
export const signUpPath = '/sign-up'
export const logInPath = '/log-in'
export const forgotPasswordPath = '/forgot-password'
export const resetPasswordPath = '/reset-password'
export const verifyEmailPath = '/verify-email'

/**
 * Where to go after logging in: `next` when it is a path inside this app, else the dashboard. Never another site
 * ("//evil.com", "/\\evil.com" or "https://…"), so a crafted log-in link can't send the user away.
 */
export function safeNext(next: string | null | undefined): string {
  if (!next || !next.startsWith('/') || next.startsWith('//') || next.startsWith('/\\')) return dashboardPath
  return next
}

/** Log-in, coming back to `from` afterwards. */
export const logInPathFor = (from: string) => `${logInPath}?next=${encodeURIComponent(from)}`
