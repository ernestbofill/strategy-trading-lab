import { useCallback, useEffect, useId, useState, type FormEvent, type ReactNode } from 'react'
import { api, InvalidValueError, TooManyTriesError, UnauthorizedError, type AdminSetting } from '../../api'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import styles from './AdminSettings.module.css'

type State = { status: 'loading' } | { status: 'logIn' } | { status: 'error' } | { status: 'ready'; settings: AdminSetting[] }

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

  const logOut = async () => {
    await api.adminLogOut()
    setState({ status: 'logIn' })
  }

  const top = (
    <TopBar
      title="STL // ADMIN"
      aside={
        state.status === 'ready' && (
          <button type="button" className={`ui-strong ${styles.textButton}`} onClick={logOut}>
            Log out
          </button>
        )
      }
    />
  )

  if (state.status === 'logIn') return <LogIn top={top} onDone={load} />
  return (
    <Screen top={top}>
      <PageHeader title="Settings" lead="Values set here win over the server's. Secrets are never shown." />
      {state.status === 'loading' && <ScreenMessage>Loading the settings…</ScreenMessage>}
      {state.status === 'error' && <ScreenMessage>Could not load the settings. Try again later.</ScreenMessage>}
      {state.status === 'ready' &&
        groups(state.settings).map(([group, settings]) => (
          <section key={group} className={styles.group}>
            <h2 className={styles.groupTitle}>{group}</h2>
            {settings.map((s) => (
              // Keyed by its value, so a row starts again from what the server now has after a save.
              <SettingRow
                key={`${s.key}:${s.source}:${s.value}:${s.isSet}`}
                setting={s}
                shown={shownValues(state.settings)} onChanged={load} onLoggedOut={() => setState({ status: 'logIn' })} />
            ))}
          </section>
        ))}
    </Screen>
  )
}

async function fetchSettings(): Promise<State> {
  try {
    return { status: 'ready', settings: await api.getAdminSettings() }
  } catch (e) {
    return e instanceof UnauthorizedError ? { status: 'logIn' } : { status: 'error' }
  }
}

/** The settings by group, in the order the server lists them. */
function groups(settings: AdminSetting[]): [string, AdminSetting[]][] {
  const byGroup = new Map<string, AdminSetting[]>()
  for (const s of settings) byGroup.set(s.group, [...(byGroup.get(s.group) ?? []), s])
  return [...byGroup]
}

function LogIn({ top, onDone }: { top: ReactNode; onDone: () => Promise<void> }) {
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const id = useId()

  const submit = async (e?: FormEvent) => {
    e?.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await api.adminLogIn(password)
      await onDone()
    } catch (err) {
      setError(
        err instanceof TooManyTriesError
          ? 'Too many tries. Wait 15 minutes and try again.'
          : err instanceof UnauthorizedError
            ? 'That password is wrong.'
            : "Couldn't log in. Try again.",
      )
      setBusy(false)
    }
  }

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={submit} disabled={busy || !password}>
          {busy ? 'Logging in…' : 'Log in'}
        </PrimaryAction>
      }
    >
      <PageHeader title="Admin" />
      <form className={styles.field} onSubmit={submit}>
        <label htmlFor={id} className="ui-strong">
          Password
        </label>
        <input
          id={id}
          className={styles.input}
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        {error && (
          <p role="alert" className={styles.error}>
            {error}
          </p>
        )}
      </form>
    </Screen>
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
  onLoggedOut: () => void
}

function SettingRow({ setting: s, shown, onChanged, onLoggedOut }: SettingRowProps) {
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
      if (e instanceof UnauthorizedError) return onLoggedOut()
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
