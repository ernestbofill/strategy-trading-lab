import { InvalidValueError, UnauthorizedError } from '../errors'
import type { AdminSetting } from '../types'

// MOCK: the admin page's settings, as the backend's Admin/SettingDefinitions.cs declares them. Password "STL".

type Definition = Pick<AdminSetting, 'key' | 'group' | 'description' | 'default' | 'secret'>

const gmail = 'nest.u333@gmail.com'

const definitions: Definition[] = [
  { key: 'SMTP_USER', group: 'Email', description: 'The email account that sends the emails.', default: gmail, secret: false },
  { key: 'SMTP_HOST', group: 'Email', description: 'The mail server.', default: 'smtp.gmail.com', secret: false },
  { key: 'SMTP_PORT', group: 'Email', description: "The mail server's port (STARTTLS).", default: '587', secret: false },
  { key: 'SMTP_FROM', group: 'Email', description: 'The sender address.', default: gmail, secret: false },
  {
    key: 'SMTP_PASSWORD',
    group: 'Email',
    description: "The account's password: for Gmail, an app password. Changing the account, server or port above clears it.",
    default: null,
    secret: true,
  },
]

/** Changing any of these clears the password, as the API does. */
const boundToPassword = ['SMTP_USER', 'SMTP_HOST', 'SMTP_PORT']

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

const effective = (key: string) =>
  values.has(key) ? (values.get(key) ?? null) : (definitions.find((d) => d.key === key)?.default ?? null)

/** Applies a change, then clears the password if the account, server or port now differs, as the API does. */
function change(apply: () => void) {
  const before = boundToPassword.map(effective)
  apply()
  if (boundToPassword.some((key, i) => effective(key) !== before[i])) values.set('SMTP_PASSWORD', null)
}

export function mockSetAdminSetting(key: string, value: string) {
  requireAdmin()
  const secret = key === 'SMTP_PASSWORD'
  const v = secret ? value : value.trim()
  if (!v.trim()) throw new InvalidValueError('Type a value, or use Default.')
  const port = Number(v)
  if (key === 'SMTP_PORT' && !(Number.isInteger(port) && port >= 1 && port <= 65535))
    throw new InvalidValueError('Use a port from 1 to 65535.')
  if ((key === 'SMTP_USER' || key === 'SMTP_FROM') && !/^[^@\s]+@[^@\s]+$/.test(v)) throw new InvalidValueError('Use an email address.')
  change(() => values.set(key, v))
}

export function mockResetAdminSetting(key: string, clear?: boolean) {
  requireAdmin()
  change(() => (clear ? values.set(key, null) : values.delete(key)))
}
