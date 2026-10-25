import { useCallback, useEffect, useId, useState, type FormEvent } from 'react'
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
import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import styles from './AdminSettings.module.css'
import { DatabaseBox } from './DatabaseBox'
import { FeedbackSection } from '../feedback/FeedbackSection'
import { PricesBox } from './PricesBox'

type State =
  | { status: 'loading' }
  | { status: 'error' }
  /** `settings` is null when they can't be read: they live in the database in use, which may be the broken one. */
  | { status: 'ready'; settings: AdminSetting[] | null; databases: AdminDatabases; version: BackendVersion | null }

/** Admin settings (/admin): settings the app reads, set here instead of in the server's environment. Not linked from the app. */
export function AdminSettings() {
  const [state, setState] = useState<State>({ status: 'loading' })

  const load = useCallback(async () => setState(await fetchSettings()), [])

  useEffect(() => {
    let live = true
    void fetchSettings().then((next) => live && setState(next))
    return () => {
      live = false
    }
  }, [])

  return (
    <Screen top={<TopBar title="STL // ADMIN" />}>
      <PageHeader title="Settings" lead="Values set here win over the server's. Secrets are never shown." />
      {state.status === 'loading' && <ScreenMessage>Loading the settings…</ScreenMessage>}
      {state.status === 'error' && <ScreenMessage>Could not load the settings. Try again later.</ScreenMessage>}
      {state.status === 'ready' && state.version && <BackendBox version={state.version} />}
      {state.status === 'ready' && (
        <DatabaseBox databases={state.databases} onChanged={load} />
      )}
      {state.status === 'ready' && !state.settings && (
        <ScreenMessage>Could not load the settings: the database in use may not answer. Switch above, or try again later.</ScreenMessage>
      )}
      {state.status === 'ready' &&
        state.settings &&
        groups(state.settings).map(([group, settings]) => (
          <section key={group} className={styles.group}>
            <h2 className={styles.groupTitle}>{group}</h2>
            {settings.map((s) => (
              // Keyed by its value, so a row starts again from what the server now has after a save.
              <SettingRow
                key={`${s.key}:${s.source}:${s.value}:${s.isSet}`}
                setting={s}
                shown={shownValues(state.settings ?? [])} onChanged={load} />
            ))}
            {group === 'Email' && <TestEmail />}
            {/* Keyed by the schedule, so the box asks again (next run) after a save. */}
            {group === 'Prices' && <PricesBox key={settings.map((s) => s.value).join('|')} />}
          </section>
        ))}
      {state.status === 'ready' && <FeedbackSection />}
    </Screen>
  )
}

async function fetchSettings(): Promise<State> {
  try {
    // The database box and the build don't need the database, so they show even when the settings can't load.
    const settings = api.getAdminSettings().catch((e: unknown) => {
      if (e instanceof UnauthorizedError || e instanceof ForbiddenError) throw e
      return null
    })
    const [databases, version, loaded] = await Promise.all([api.getAdminDatabases(), api.getVersion().catch(() => null), settings])
    return { status: 'ready', settings: loaded, databases, version }
  } catch {
    return { status: 'error' }
  }
}

/** The API's build, for information. */
function BackendBox({ version }: { version: BackendVersion }) {
  const rows: [string, string][] = [
    ['Version', version.version],
    ['Commit', version.commit?.slice(0, 7) ?? 'unknown'],
    ['Built', version.builtAt ? `${version.builtAt.slice(0, 16).replace('T', ' ')} UTC` : 'unknown (a Debug build)'],
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

/** "Send a test email": to the mail account itself, with the settings as saved. */
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
  const [draft, setDraft] = useState(s.secret ? '' : (s.value ?? ''))
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
        <label htmlFor={id} className={styles.key}>
          {s.key}
        </label>
        <p className={styles.description}>{s.description}</p>
        <p className={styles.source}>{sourceText[s.source]}</p>
      </div>
      <div className={styles.controls}>
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
