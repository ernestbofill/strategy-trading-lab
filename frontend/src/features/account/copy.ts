import { TooManyTriesError } from '../../api'
import { tNow } from '../../i18n/I18n'

// The account screens' messages (spec: Sign-up, Log-in, Forgot and reset password).
export const EXPIRED_LINK = tNow('account.expiredLink')

/** What to tell the user when a request failed for a reason the screen doesn't handle itself. */
export const errorText = (err: unknown): string =>
  err instanceof TooManyTriesError ? tNow('account.error.tooManyTries') : tNow('account.error.generic')
