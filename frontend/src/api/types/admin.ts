import type { Locale, Role } from './account'
import type { AssetKind } from './assets'
import type { InvestsIn, TemplateKey } from './templates'

/** A setting on the admin page. A secret's `value` is never sent: only whether it is set. */
export interface AdminSetting {
  key: string
  group: string
  description: string
  default: string | null
  secret: boolean
  value: string | null
  isSet: boolean
  /** Where the value comes from: this page, the server's environment, the default, or nowhere. */
  source: 'page' | 'server' | 'default' | 'none'
  /** How the page asks for it: a text box, or the time zone list of Settings. */
  input: 'text' | 'timeZone'
}

/** Someone who signed up with an invite. */
export interface InvitedUser {
  email: string
  firstName: string | null
  lastName: string | null
  joinedAt: string
}

/** An invite on /admin/invites: backend Invites/InviteEndpoints.cs (AdminInvite). */
export interface AdminInvite {
  id: string
  code: string
  link: string
  /** Ready to copy into WhatsApp or anywhere else, in the invite's language. */
  message: string
  firstName: string
  lastName: string | null
  email: string | null
  locale: Locale
  role: Role
  maxUses: number
  uses: number
  createdAt: string
  sentAt: string | null
  revokedAt: string | null
  joined: InvitedUser[]
}

export interface NewInvite {
  firstName: string
  lastName?: string
  email?: string
  locale: Locale
  /** Also email it to `email`. */
  send?: boolean
}

export type DatabaseName = 'dev' | 'production'

/** A database on the admin page. Its URL is set only on the server and holds a password, so only the host is sent. */
export interface AdminDatabase {
  name: DatabaseName
  /** The server variable its URL comes from: DATABASE_URL for the server's own, else DATABASE_URL_DEV or _PRODUCTION. */
  variable: string
  /** Null when the variable isn't set. */
  host: string | null
}

export interface AdminDatabases {
  active: DatabaseName
  /** The server's own database, the one its environment points at. */
  home: DatabaseName
  databases: AdminDatabase[]
}

/** A symbol the last price refresh couldn't update. */
export interface PriceProblem {
  name: string
  problem: string | null
}

/** The admin page's prices box: backend Admin/AdminEndpoints.cs (AdminPrices). Times are ISO strings, days "2026-10-22". */
export interface AdminPrices {
  running: boolean
  /** Null while the daily schedule is off. */
  nextRun: string | null
  lastStarted: string | null
  lastFinished: string | null
  latestDay: string | null
  problems: PriceProblem[]
  updated: number
  fullHistory: number
  /** Signals the rules created after the last refresh; null before the first run or when the evaluation failed. */
  newSignals: number | null
  /** Strategies the last evaluation couldn't run. */
  evaluationFailures: number
}

/** What Yahoo answered for one symbol in "Test Yahoo". */
export interface YahooCheck {
  symbol: string
  ok: boolean
  day: string | null
  close: number | null
  problem: string | null
  milliseconds: number
}

/** An account on the admin users page: backend Admin/AdminUsers.cs (AdminUser). */
export interface AdminUser {
  id: string
  email: string
  role: Role
  joined: string
  confirmed: boolean
  /** Why the mail service says the address can't be mailed (the app no longer sends it the overview or alerts); null when it can. */
  emailBlocked: 'bounced' | 'invalid' | 'complaint' | null
  /** The last time any of its sessions was used; null when none is kept. */
  lastVisit: string | null
  running: number
  archived: number
  firstName: string | null
  lastName: string | null
  /** The email of whoever made the invite it signed up with; null without one. */
  invitedBy: string | null
}

/** Running strategies of one template, by mode. */
export interface AdminTemplateCount {
  templateKey: TemplateKey
  templateName: string
  simulation: number
  live: number
}

/** The admin users page: backend Admin/AdminUsers.cs (AdminUsers). Users newest first. */
export interface AdminUsers {
  counts: { users: number; confirmed: number; running: AdminTemplateCount[] }
  users: AdminUser[]
}

/** What raised an admin alert. */
export type AlertSource = 'dailyCheck' | 'prices' | 'strategies' | 'aiStrategies' | 'email'

/** Open; dismissed (quiet while the problem lasts); or cleared, when a later run found the problem gone. */
export type AlertStatus = 'open' | 'dismissed' | 'cleared'

/** A problem the background jobs found: backend Admin/AdminAlerts.cs (AdminAlertDto). `count`: times seen since it opened. */
export interface AdminAlert {
  id: string
  source: AlertSource
  status: AlertStatus
  title: string
  detail: string | null
  count: number
  firstSeenAt: string
  lastSeenAt: string
  closedAt: string | null
}

/** A template on /admin/templates: backend Admin/AdminTemplates.cs (AdminTemplate). Its settings and every version, newest first. */
export interface AdminTemplate {
  key: string
  name: string
  investsIn: InvestsIn
  /** False for a template whose stocks have no buy rule (the AI's): no "getting close". */
  hasClosePct: boolean
  settings: AdminTemplateSetting[]
  versions: AdminTemplateVersion[]
}

/** A setting of Advanced settings, with its words (`sentence` with `{value}`, `value` with `{n}`) and range. */
export interface AdminTemplateSetting {
  key: string
  sentence: string
  value: string
  step: number
  min: number
  max: number
  below?: string | null
}

/** A version of what new strategies start with. Version 1 is the tested one; one version is active. */
export interface AdminTemplateVersion {
  version: number
  active: boolean
  createdAt: string
  defaults: Record<string, number>
  /** The starter list, or the one fund, sorted by name. */
  assets: AdminTemplateAsset[]
  closePct: number
  /** How many strategies started from it. */
  strategies: number
}

export interface AdminTemplateAsset {
  id: string
  symbol: string
  name: string
  active: boolean
}

/** A new version: every setting's default, the stocks or the fund, "getting close". */
export interface AdminTemplateChange {
  defaults: Record<string, number>
  assetIds: string[]
  closePct: number | null
}

/** An asset on the admin assets page: backend Admin/AdminEndpoints.cs (AdminAsset). */
export interface AdminAsset {
  id: string
  symbol: string
  exchange: string
  name: string
  what: string
  kind: AssetKind
  currency: string
  active: boolean
  /** Running strategies that list this asset, ticked or not: its prices are fetched daily. */
  strategies: number
  /** Newest stored price day ("2026-10-22"), or null if none is stored yet. */
  latestPriceDate: string | null
  /** Why the last price fetch couldn't be used (Yahoo's error, bars that aren't daily), or null when it worked. */
  pricesProblem: string | null
}

/** What deleting every price did: backend Admin/AdminEndpoints.cs (PricesFlushed). */
export interface PricesFlushed {
  prices: number
  rates: number
}

/** What downloading the exchange rates did: backend Admin/AdminEndpoints.cs (RatesDownloaded). */
export interface RatesDownloaded {
  rows: number
  /** The currencies that failed, with why. */
  problems: PriceProblem[]
}

/** What adding a symbol did: backend Admin/AdminEndpoints.cs (AdminAddedAsset). */
export interface AdminAddedAsset {
  asset: AdminAsset
  /** False when the asset was already there. */
  created: boolean
  /** Why its prices or exchange rate didn't load; adding it again tries again. */
  problem: string | null
}

/** A user a test push can go to (push on, at least one device): backend Admin/AdminNotifications.cs. */
export interface AdminPushRecipient {
  id: string
  email: string
  devices: number
}

export type NotificationKind = 'signal' | 'snoozed' | 'overview' | 'ai_round' | 'signal_ended' | 'stock_candidate' | 'stock_close' | 'test'

/** A push on /admin/notifications. `title` and `body` are null for pushes sent before 2026-11-24. */
export interface AdminPush {
  id: string
  userId: string
  email: string
  kind: NotificationKind
  title: string | null
  body: string | null
  createdAt: string
  sentAt: string | null
  failedAt: string | null
  error: string | null
}

export interface AdminNotifications {
  recipients: AdminPushRecipient[]
  /** The newest 300, newest first. */
  pushes: AdminPush[]
}

export interface TestPush {
  userId: string
  title: string
  body: string
}
