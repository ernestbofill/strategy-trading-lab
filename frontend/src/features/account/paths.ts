import { dashboardPath } from '../new-strategy/paths'

import { accountPath } from '../dashboard/paths'

export { accountPath }
/** The hash of the account page's Emails section, and the link that opens the page at it (Confirm's "Edit" on Daily overview). */
export const EMAILS_HASH = '#emails'
export const accountEmailsPath = `${accountPath}${EMAILS_HASH}`
export const signUpPath = '/sign-up'
/** The invite code in an invite link: /sign-up?invite=ABCD-EFGH. */
export const INVITE_PARAM = 'invite'
export const logInPath = '/log-in'
export const forgotPasswordPath = '/forgot-password'
export const resetPasswordPath = '/reset-password'
export const verifyEmailPath = '/verify-email'

const SAME_SITE = 'https://stl.invalid'

/**
 * Where to go after logging in: `next` when it is a path inside this app, else the dashboard. Never another site
 * ("//evil.com", "/\\evil.com", "/\t/evil.com" or "https://…"), so a crafted log-in link can't send the user away.
 * It is parsed as the browser would parse it (which drops tabs and newlines and reads "\\" as "/"), then rebuilt
 * from its path, so what is followed is what was checked.
 */
export function safeNext(next: string | null | undefined): string {
  if (!next?.startsWith('/')) return dashboardPath
  try {
    const url = new URL(next, SAME_SITE)
    // Dot segments are resolved after the origin: "/a/..//evil.com" becomes the path "//evil.com".
    if (url.origin !== SAME_SITE || url.pathname.startsWith('//')) return dashboardPath
    return `${url.pathname}${url.search}${url.hash}`
  } catch {
    return dashboardPath
  }
}

/** Log-in, coming back to `from` afterwards. */
export const logInPathFor = (from: string) => `${logInPath}?next=${encodeURIComponent(from)}`
