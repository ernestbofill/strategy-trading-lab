import { InvalidValueError, UnauthorizedError } from '../errors'
import type { AdminDatabase, AdminDatabases, AdminSetting, DatabaseName } from '../types'

// MOCK: the admin page's settings, as the backend's Admin/SettingDefinitions.cs declares them. Password "STL".

type Definition = Pick<AdminSetting, 'key' | 'group' | 'description' | 'default' | 'secret'>

const gmail = 'nest.u333@gmail.com'

const definitions: Definition[] = [
  { key: 'SMTP_USER', group: 'Email', description: 'The email account that sends the emails.', default: gmail, secret: false },
  { key: 'SMTP_HOST', group: 'Email', description: 'The mail server.', default: 'smtp.gmail.com', secret: false },
  { key: 'SMTP_PORT', group: 'Email', description: "The mail server's port (STARTTLS).", default: '587', secret: false },
  { key: 'SMTP_FROM', group: 'Email', description: 'The sender address. Empty: the account above.', default: null, secret: false },
  {
    key: 'SMTP_PASSWORD',
    group: 'Email',
    description: "The account's password: for Gmail, an app password. It only counts while the settings above stay as they were when it was saved.",
    default: null,
    secret: true,
  },
]

/** The password only counts while these keep the values they had when it was saved, as in the API. */
const boundToPassword = ['SMTP_USER', 'SMTP_HOST', 'SMTP_PORT', 'SMTP_FROM']

/** Values set on the page; null hides the default. */
const values = new Map<string, string | null>()
/** The password and the bound values it was saved with. */
let password: { value: string; binding: string } | null = null
let loggedIn = false

const effective = (key: string) =>
  values.has(key) ? (values.get(key) ?? null) : (definitions.find((d) => d.key === key)?.default ?? null)
const binding = () => boundToPassword.map(effective).join('\u001f')
const passwordIsSet = () => password !== null && password.binding === binding()

const requireAdmin = () => {
  if (!loggedIn) throw new UnauthorizedError()
}

export function mockAdminLogIn(given: string) {
  if (given !== 'STL') throw new UnauthorizedError()
  loggedIn = true
}

export function mockAdminLogOut() {
  loggedIn = false
}

export function mockAdminSettings(): AdminSetting[] {
  requireAdmin()
  return definitions.map((d) => {
    if (d.secret) return { ...d, value: null, isSet: passwordIsSet(), source: passwordIsSet() ? 'page' : 'none' }
    const value = effective(d.key)
    const source = values.has(d.key) ? (value === null ? 'none' : 'page') : value === null ? 'none' : 'default'
    return { ...d, value, isSet: value !== null, source }
  })
}

/** The API's email check: exactly one plain address, at most 254 characters (no lists, no display name). */
const isEmail = (v: string) => v.length <= 254 && /^[^@\s,;<>"()]+@[^@\s,;<>"()]+$/.test(v)

export function mockSetAdminSetting(key: string, value: string, shown?: Record<string, string | null>) {
  requireAdmin()
  if (!value.trim()) throw new InvalidValueError('Type a value, or use Default.')
  const v = key === 'SMTP_PASSWORD' ? value : value.trim()
  if (v.length > 500) throw new InvalidValueError('Use at most 500 characters.')
  const port = Number(v)
  if (key === 'SMTP_PORT' && !(Number.isInteger(port) && port >= 1 && port <= 65535))
    throw new InvalidValueError('Use a port from 1 to 65535.')
  if ((key === 'SMTP_USER' || key === 'SMTP_FROM') && !isEmail(v)) throw new InvalidValueError('Use one email address, e.g. you@gmail.com.')
  if (key === 'SMTP_PASSWORD') {
    if (!shown || boundToPassword.some((k) => (shown[k] ?? null) !== effective(k)))
      throw new InvalidValueError('The mail settings changed since this page loaded. Reload, check them, then type the password again.')
    password = { value: v, binding: binding() }
  }
  else values.set(key, v)
}

export function mockResetAdminSetting(key: string, clear?: boolean) {
  requireAdmin()
  if (key === 'SMTP_PASSWORD') password = null
  else if (clear) values.set(key, null)
  else values.delete(key)
}

/** Like the API: only to the account itself, and only when the password is set for it. Nothing is sent. */
export function mockSendTestEmail(): string {
  requireAdmin()
  const account = effective('SMTP_USER')
  if (!account || !passwordIsSet()) throw new InvalidValueError("Email isn't set up: set the account, the server and the password first.")
  return account
}

// MOCK: the database box, as the backend's Admin/DatabaseSwitch.cs. Every Neon URL "answers".

const databaseUrls = new Map<DatabaseName, string>()
let activeDatabase: DatabaseName = 'dev'
/** The mock stands in for a local API: its own database is dev, from the "environment". */
const serverDevHost = 'ep-mock-dev-pooler.eu-central-1.aws.neon.tech'

const hostOf = (url: string) => new URL(url.replace(/^postgres(ql)?:/, 'http:')).hostname

export const mockActiveDatabase = () => activeDatabase

export function mockAdminDatabases(): AdminDatabases {
  requireAdmin()
  const databases = (['dev', 'production'] as const).map((name): AdminDatabase => {
    const url = databaseUrls.get(name)
    if (url) return { name, source: 'page', host: hostOf(url) }
    return name === 'dev' ? { name, source: 'server', host: serverDevHost } : { name, source: 'none', host: null }
  })
  return { active: activeDatabase, home: 'dev', databases }
}

const hasUrl = (name: DatabaseName) => name === 'dev' || databaseUrls.has(name)

export function mockSwitchDatabase(name: DatabaseName) {
  requireAdmin()
  if (!hasUrl(name)) throw new InvalidValueError(`The ${name} database has no URL: set one first.`)
  activeDatabase = name
}

export function mockSetDatabaseUrl(name: DatabaseName, url: string) {
  requireAdmin()
  const v = url.trim()
  if (!/^postgres(ql)?:\/\/[^:@/]+:[^@/]+@[^/]+\/[^/?]+/.test(v))
    throw new InvalidValueError("Use the Neon branch's DATABASE_URL: postgresql://user:password@ep-….neon.tech/neondb?sslmode=require.")
  if (!hostOf(v).endsWith('.neon.tech')) throw new InvalidValueError('Use a Neon database (a host ending in .neon.tech).')
  databaseUrls.set(name, v)
}

export function mockResetDatabaseUrl(name: DatabaseName) {
  requireAdmin()
  if (name === activeDatabase && name !== 'dev') throw new InvalidValueError(`The ${name} database has no URL: set one first.`)
  databaseUrls.delete(name)
}
