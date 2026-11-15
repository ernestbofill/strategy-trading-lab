import { useId, useState, type FormEvent } from 'react'
import {
  api,
  InvalidValueError,
  ForbiddenError,
  TooManyTriesError,
  UnauthorizedError,
  type AdminDatabases,
  type AdminSetting,
  type BackendVersion,
} from '../../api'
import { Loaded } from '../../components/Loaded'
import { NavRow } from '../../components/NavRow'
import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { localDateTime } from '../../lib/format'
import { invalidate, reload, useLoadable } from '../../lib/useLoadable'
import { TimeZoneField } from '../account/TimeZoneField'
import { adminCacheKeys, invalidateAdmin } from './cacheKeys'
import styles from './AdminSettings.module.css'
import { DatabaseBox } from './DatabaseBox'
import { PricesBox } from './PricesBox'

interface AdminPage {
  /** Null when they can't be read: they live in the database in use, which may be the broken one. */
  settings: AdminSetting[] | null
  databases: AdminDatabases
  version: BackendVersion | null
}

/** How many alerts are open, for the Alerts row; null until known, or when they can't be read. */
function useOpenAlerts() {
  const alerts = useLoadable(adminCacheKeys.alerts, api.getAdminAlerts)
  return alerts.status === 'ready' ? alerts.data.filter((a) => a.status === 'open').length : null
}

/** Admin settings (/admin): settings the app reads, set here instead of in the server's environment. Not linked from the app. */
export function AdminSettings() {
  const state = useLoadable(adminCacheKeys.settings, fetchSettings)
  // After a save or a switch: keeps the page on screen while it loads again, the prices box's next run included.
  const load = async () => {
    invalidate(adminCacheKeys.prices)
    await reload(adminCacheKeys.settings)
  }
  // Another database: every admin page's rows were the other one's.
  const switched = async () => {
    invalidateAdmin()
    await reload(adminCacheKeys.settings)
  }
  const openAlerts = useOpenAlerts()

  return (
    <Screen top={<TopBar home />}>
      <PageHeader title="Admin" lead="Values set here win over the server's. Secrets are never shown." />
      <section className={styles.group}>
        <h2 className={styles.groupTitle}>Management</h2>
        <NavRow
          label="Alerts"
          meta={openAlerts === null ? 'problems the daily jobs found' : openAlerts === 0 ? 'none open' : `${openAlerts} open`}
          to="/admin/alerts"
        />
        <NavRow label="Users" meta="accounts and their strategies" to="/admin/users" />
        <NavRow label="Invites" meta="invite people and choose who can sign up" to="/admin/invites" />
        <NavRow label="Assets" meta="stocks, funds and indexes" to="/admin/assets" />
        <NavRow label="Templates" meta="defaults, starter lists and getting close" to="/admin/templates" />
        <NavRow label="Feedback" meta="open issues, ideas and notes" to="/feedback" />
      </section>
      <Loaded state={state} what="the settings">
        {(page) => (
          <>
            {page.version && <BackendBox version={page.version} />}
            <DatabaseBox databases={page.databases} onChanged={switched} />
            {!page.settings && (
              <ScreenMessage>Could not load the settings: the database in use may not answer. Switch above, or try again later.</ScreenMessage>
            )}
            {page.settings &&
              groups(page.settings).map(([group, settings]) => (
                <section key={group} className={styles.group}>
                  <h2 className={styles.groupTitle}>{group}</h2>
                  {settings.map((s) => (
                    // Keyed by its value, so a row starts again from what the server now has after a save.
                    <SettingRow key={`${s.key}:${s.source}:${s.value}:${s.isSet}`} setting={s} shown={shownValues(page.settings ?? [])} onChanged={load} />
                  ))}
                  {group === 'Email' && <TestEmail />}
                  {group === 'Prices' && <PricesBox />}
                </section>
              ))}
          </>
        )}
      </Loaded>
    </Screen>
  )
}

async function fetchSettings(): Promise<AdminPage> {
  // The database box and the build don't need the database, so they show even when the settings can't load.
  const settings = api.getAdminSettings().catch((e: unknown) => {
    if (e instanceof UnauthorizedError || e instanceof ForbiddenError) throw e
    return null
  })
  const [databases, version, loaded] = await Promise.all([api.getAdminDatabases(), api.getVersion().catch(() => null), settings])
  return { settings: loaded, databases, version }
}

/** The API's build, for information. */
function BackendBox({ version }: { version: BackendVersion }) {
  const rows: [string, string][] = [
    ['Version', version.version],
    ['Commit', version.commit?.slice(0, 7) ?? 'unknown'],
    ['Built', version.builtAt ? localDateTime(version.builtAt) : 'unknown (a Debug build)'],
  ]
  return (
    <section className={styles.group}>
      <h2 className={styles.groupTitle}>Backend</h2>
      <dl className={styles.facts}>
        {rows.map(([label, value]) => (
          <div key={label} className={styles.fact}>
            <dt className={styles.description}>{label}</dt>
            <dd className={styles.key}>{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}

/** The settings by group, in the order the server lists them. */
function groups(settings: AdminSetting[]): [string, AdminSetting[]][] {
  const byGroup = new Map<string, AdminSetting[]>()
  for (const s of settings) byGroup.set(s.group, [...(byGroup.get(s.group) ?? []), s])
  return [...byGroup]
}

/** "Send a test email": to the signed-in owner, with the settings as saved. */
function TestEmail() {
  const [result, setResult] = useState<{ ok: boolean; text: string } | null>(null)
  const [busy, setBusy] = useState(false)

  const send = async () => {
    setBusy(true)
    setResult(null)
    try {
      const sentTo = await api.sendAdminTestEmail()
      setResult({ ok: true, text: `Sent to ${sentTo}. Check its inbox.` })
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setResult({
        ok: false,
        text:
          e instanceof TooManyTriesError
            ? 'Too many test emails. Try again in an hour.'
            : e instanceof InvalidValueError
              ? e.message
              : "Couldn't send it. Try again.",
      })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={styles.row}>
      <div className={styles.text}>
        <span className={styles.key}>Test email</span>
        <p className={styles.description}>Sends one email to the account above, with the settings as saved.</p>
      </div>
      <div className={styles.controls}>
        <div className={styles.buttons}>
          <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={send}>
            {busy ? 'Sending…' : 'Send a test email'}
          </button>
        </div>
        {result && (
          <p role={result.ok ? 'status' : 'alert'} className={result.ok ? styles.done : styles.error}>
            {result.text}
          </p>
        )}
      </div>
    </div>
  )
}

const sourceText: Record<AdminSetting['source'], string> = {
  page: 'Set here',
  server: "From the server's environment",
  default: 'Default',
  none: 'Not set',
}

/** The values the page shows, sent with a secret so the API can check its mail server hasn't changed since. */
const shownValues = (settings: AdminSetting[]) => Object.fromEntries(settings.map((s) => [s.key, s.value]))

interface SettingRowProps {
  setting: AdminSetting
  shown: Record<string, string | null>
  onChanged: () => Promise<void>
}

function SettingRow({ setting: s, shown, onChanged }: SettingRowProps) {
  const timeZone = s.input === 'timeZone'
  // A time zone always shows a row, so its draft starts on the zone shown (the default when none is set), never on ''.
  const [draft, setDraft] = useState(s.secret ? '' : (s.value ?? (timeZone ? s.default : null) ?? ''))
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const id = useId()
  const edited = s.secret ? draft !== '' : draft !== (s.value ?? '')

  const run = async (action: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    try {
      await action()
      await onChanged()
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setError(e instanceof InvalidValueError ? e.message : "Couldn't save it. Try again.")
    } finally {
      setBusy(false)
    }
  }

  const save = (e?: FormEvent) => {
    e?.preventDefault()
    if (edited) void run(() => api.setAdminSetting(s.key, draft, s.secret ? shown : undefined))
  }

  const placeholder = s.secret ? (s.isSet ? 'set (hidden): type to replace' : 'not set') : (s.default ?? '')

  return (
    <form className={styles.row} onSubmit={save}>
      <div className={styles.text}>
        {timeZone ? (
          <span className={styles.key}>{s.key}</span>
        ) : (
          <label htmlFor={id} className={styles.key}>
            {s.key}
          </label>
        )}
        <p className={styles.description}>{s.description}</p>
        <p className={styles.source}>{sourceText[s.source]}</p>
      </div>
      <div className={styles.controls}>
        {timeZone ? (
          <TimeZoneField label={s.key} hideLabel saved={s.value ?? s.default ?? ''} value={draft} onChange={setDraft} />
        ) : (
          <input
            id={id}
            className={styles.input}
            type={s.secret ? 'password' : 'text'}
            autoComplete={s.secret ? 'new-password' : 'off'}
            spellCheck={false}
            value={draft}
            placeholder={placeholder}
            onChange={(e) => setDraft(e.target.value)}
          />
        )}
        <div className={styles.buttons}>
          <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || !edited}>
            Save
          </button>
          {s.secret ? (
            <button
              type="button"
              className={`ui-strong ${styles.button}`}
              disabled={busy || !s.isSet}
              onClick={() => run(() => api.resetAdminSetting(s.key, true))}
            >
              Clear
            </button>
          ) : (
            <button
              type="button"
              className={`ui-strong ${styles.button}`}
              disabled={busy || s.source !== 'page'}
              onClick={() => run(() => api.resetAdminSetting(s.key))}
            >
              Default
            </button>
          )}
        </div>
        {error && (
          <p role="alert" className={styles.error}>
            {error}
          </p>
        )}
      </div>
    </form>
  )
}
