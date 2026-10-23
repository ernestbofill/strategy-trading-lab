import { TooManyTriesError } from '../../api'

// The account screens' messages (spec: Sign-up, Log-in, Forgot and reset password).
export const TOO_MANY_TRIES = 'Too many tries. Wait a few minutes and try again.'
export const EXPIRED_LINK = 'This link has expired. Ask for a new one.'

/** What to tell the user when a request failed for a reason the screen doesn't handle itself. */
export const errorText = (err: unknown): string =>
  err instanceof TooManyTriesError ? TOO_MANY_TRIES : 'Something went wrong. Try again.'
