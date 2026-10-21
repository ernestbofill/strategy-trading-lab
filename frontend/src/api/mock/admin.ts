import { InvalidValueError, UnauthorizedError } from '../errors'
import type { AdminSetting } from '../types'

// MOCK: the admin page's settings, as the backend's Admin/SettingDefinitions.cs declares them. Password "STL".

type Definition = Pick<AdminSetting, 'key' | 'group' | 'description' | 'default' | 'secret'>

const definitions: Definition[] = [
  { key: 'SMTP_USER', group: 'Email', description: 'The email account that sends the emails, e.g. you@gmail.com.', default: null, secret: false },
  { key: 'SMTP_PASSWORD', group: 'Email', description: 'Its password. For Gmail, an app password.', default: null, secret: true },
  { key: 'SMTP_HOST', group: 'Email', description: 'The mail server. Empty with a @gmail.com user: smtp.gmail.com.', default: null, secret: false },
  { key: 'SMTP_PORT', group: 'Email', description: "The mail server's port (STARTTLS).", default: '587', secret: false },
  { key: 'SMTP_FROM', group: 'Email', description: 'The sender address. Empty: SMTP_USER.', default: null, secret: false },
]

/** Values set on the page; null is a cleared secret. */
const values = new Map<string, string | null>()
let loggedIn = false

const requireAdmin = () => {
  if (!loggedIn) throw new UnauthorizedError()
}

export function mockAdminLogIn(password: string) {
  if (password !== 'STL') throw new UnauthorizedError()
  loggedIn = true
}

export function mockAdminLogOut() {
  loggedIn = false
}

export function mockAdminSettings(): AdminSetting[] {
  requireAdmin()
  return definitions.map((d) => {
    const fromPage = values.has(d.key)
    const value = fromPage ? (values.get(d.key) ?? null) : d.default
    const source = fromPage ? (value === null ? 'none' : 'page') : d.default === null ? 'none' : 'default'
    return { ...d, value: d.secret ? null : value, isSet: value !== null, source }
  })
}

export function mockSetAdminSetting(key: string, value: string) {
  requireAdmin()
  const port = Number(value.trim())
  if (key === 'SMTP_PORT' && !(Number.isInteger(port) && port >= 1 && port <= 65535))
    throw new InvalidValueError('Use a port from 1 to 65535.')
  if (!value.trim()) throw new InvalidValueError('Type a value, or use Default.')
  values.set(key, value.trim())
}

export function mockResetAdminSetting(key: string, clear?: boolean) {
  requireAdmin()
  if (clear) values.set(key, null)
  else values.delete(key)
}
