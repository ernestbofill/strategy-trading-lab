import type { DatabaseName } from './admin'

/** What the browser's `PushSubscription.toJSON()` gives: backend Notifications/WebPushEndpoints.cs. */
export interface PushSubscriptionJson {
  endpoint: string
  keys: { p256dh: string; auth: string }
}

export type Locale = 'en' | 'ca'

/** The ISO code of a currency a user can pick (see `CURRENCIES` in `lib/format.ts`). */
export type Currency = 'EUR' | 'USD' | 'GBP'

/** What an account may do: the owner runs the app (the admin page), a vip is invited, a user is everyone else. */
export type Role = 'user' | 'vip' | 'owner'

/** The logged-in user: backend Auth/AccountEndpoints.cs (AccountDto). */
export interface Account {
  email: string
  /** Asked at sign-up since invites; older accounts have none until they add it. */
  firstName: string | null
  lastName: string | null
  emailVerified: boolean
  locale: Locale
  timeZone: string
  /** The summary by email, after the US market close on days something changed. */
  overviewEmail: boolean
  /** Signal alerts and reminders by email. */
  alertEmails: boolean
  /** Web push on the devices the user turned on: the one push switch (signals, stock changes, reminders). */
  alertPush: boolean
  /** Of the user's new strategies; running ones keep theirs. */
  currency: Currency
  role: Role
  /** Shares AI rounds anonymously. */
  aiSharing: boolean
  /** The hour (0 to 23, the user's time zone) alerts, the summary and reminders come; null: as soon as they're ready. */
  notifyHour: number | null
  termsVersionAccepted?: string | null
  termsAcceptedAt?: string | null
}

/** What Sign-up sends. */
export interface SignUp {
  firstName: string
  lastName: string
  email: string
  password: string
  locale: Locale
  /** IANA, e.g. "Europe/Madrid". */
  timeZone: string
  currency: Currency
  disclaimerAccepted: boolean
  /** The accepted legal terms version. */
  termsVersion?: string
  /** From an invite link or typed; needed while sign-ups are invite only. */
  inviteCode?: string
}

/** Who an invite is for: Sign-up greets them and fills in what is known. */
export interface SignUpInvite {
  code: string
  firstName: string
  locale: Locale
  /** The inviter's first name, when they have one. */
  invitedBy: string | null
}

/** What Sign-up asks first: backend Auth/AccountEndpoints.cs (SignUpOptions). */
export interface SignUpOptions {
  inviteOnly: boolean
  /** Null without a code, or when the code can't be used (revoked or used up). */
  invite: SignUpInvite | null
}

/** The API's build, and the database it uses (the top bar shows DEV for the dev one). */
export interface BackendVersion {
  version: string
  commit: string | null
  /** ISO time; null in Debug builds. */
  builtAt: string | null
  database: DatabaseName
}
