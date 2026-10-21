import { InvalidValueError, UnauthorizedError } from '../errors'
import type { AdminSetting } from '../types'

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
    description: "The account's password: for Gmail, an app password. It only counts while the account, server and port above stay as they were when it was saved.",
    default: null,
    secret: true,
  },
]

/** The password only counts while these keep the values they had when it was saved, as in the API. */
const boundToPassword = ['SMTP_USER', 'SMTP_HOST', 'SMTP_PORT']

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

/** The API's email check: something before and after one '@', no spaces, at most 254 characters. */
const isEmail = (v: string) => v.length <= 254 && v.indexOf('@') > 0 && v.indexOf('@') < v.length - 1 && !/\s/.test(v)

export function mockSetAdminSetting(key: string, value: string, shown?: Record<string, string | null>) {
  requireAdmin()
  if (!value.trim()) throw new InvalidValueError('Type a value, or use Default.')
  const v = key === 'SMTP_PASSWORD' ? value : value.trim()
  if (v.length > 500) throw new InvalidValueError('Use at most 500 characters.')
  const port = Number(v)
  if (key === 'SMTP_PORT' && !(Number.isInteger(port) && port >= 1 && port <= 65535))
    throw new InvalidValueError('Use a port from 1 to 65535.')
  if ((key === 'SMTP_USER' || key === 'SMTP_FROM') && !isEmail(v)) throw new InvalidValueError('Use an email address.')
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
  if (!account) throw new InvalidValueError('Set the email account first.')
  if (!passwordIsSet()) throw new InvalidValueError("Email isn't set up: set the account, the server and the password first.")
  return account
}
