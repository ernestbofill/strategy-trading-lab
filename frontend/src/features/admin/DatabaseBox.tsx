import { useId, useState, type FormEvent } from 'react'
import { api, InvalidValueError, UnauthorizedError, type AdminDatabase, type AdminDatabases, type DatabaseName } from '../../api'
import { refreshBackendVersion } from '../../lib/backendVersion'
import styles from './AdminSettings.module.css'

const nameText: Record<DatabaseName, string> = { dev: 'Dev', production: 'Production' }

const sourceText: Record<AdminDatabase['source'], string> = {
  page: 'Set here',
  server: "From the server's environment",
  none: 'Not set',
}

/** Runs a change with a busy flag and the reason it was refused; a lapsed login goes back to the log-in form. */
function useChange(onLoggedOut: () => void, failure: string) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const run = async (action: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    try {
      await action()
    } catch (e) {
      if (e instanceof UnauthorizedError) return onLoggedOut()
      setError(e instanceof InvalidValueError ? e.message : failure)
    } finally {
      setBusy(false)
    }
  }
  return { error, busy, run }
}

interface Props {
  databases: AdminDatabases
  onChanged: () => Promise<void>
  onLoggedOut: () => void
}

/** Which database the API uses (dev or production), and their URLs. Every change is tested by the API first. */
export function DatabaseBox({ databases, onChanged, onLoggedOut }: Props) {
  const { active, home } = databases
  const other: DatabaseName = active === 'dev' ? 'production' : 'dev'
  const { error, busy, run } = useChange(onLoggedOut, "Couldn't switch. Try again.")

  const switchTo = () =>
    run(async () => {
      await api.switchAdminDatabase(other)
      await Promise.all([onChanged(), refreshBackendVersion()])
    })

  return (
    <section className={styles.group}>
      <h2 className={styles.groupTitle}>Database</h2>
      <div className={styles.row}>
        <div className={styles.text}>
          <span className={styles.key}>In use: {nameText[active]}</span>
          <p className={styles.description}>
            Every request reads and writes this database, settings on this page included. The deploy migrates production only.
          </p>
          {active !== home && (
            <p role="note" className={styles.warning}>
              This server normally uses {nameText[home].toLowerCase()}: switch back when you are done.
            </p>
          )}
        </div>
        <div className={styles.controls}>
          <div className={styles.buttons}>
            <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={switchTo}>
              {busy ? 'Switching…' : `Use ${nameText[other].toLowerCase()}`}
            </button>
          </div>
          {error && (
            <p role="alert" className={styles.error}>
              {error}
            </p>
          )}
        </div>
      </div>
      {databases.databases.map((d) =>
        d.name === home ? (
          <ServerUrlRow key={d.name} database={d} />
        ) : (
          <UrlRow key={`${d.name}:${d.source}:${d.host}`} database={d} onChanged={onChanged} onLoggedOut={onLoggedOut} />
        ),
      )}
    </section>
  )
}

/** The server's own database: its URL is in the server's environment, the one the deploy migrates, never set here. */
function ServerUrlRow({ database: d }: { database: AdminDatabase }) {
  return (
    <div className={styles.row}>
      <div className={styles.text}>
        <span className={styles.key}>{nameText[d.name]} URL</span>
        <p className={styles.description}>This server&apos;s own database, from its environment (DATABASE_URL).</p>
        <p className={styles.source}>
          {sourceText[d.source]}
          {d.host && `: ${d.host}`}
        </p>
      </div>
    </div>
  )
}

/** The other database's URL, set here. */
function UrlRow({ database: d, onChanged, onLoggedOut }: { database: AdminDatabase } & Omit<Props, 'databases'>) {
  const [draft, setDraft] = useState('')
  const id = useId()
  const { error, busy, run: change } = useChange(onLoggedOut, "Couldn't save it. Try again.")
  const run = (action: () => Promise<void>) =>
    change(async () => {
      await action()
      await onChanged()
    })

  const save = (e: FormEvent) => {
    e.preventDefault()
    // Cleared once saved: the URL holds a password.
    if (draft.trim())
      void run(async () => {
        await api.setAdminDatabaseUrl(d.name, draft)
        setDraft('')
      })
  }

  return (
    <form className={styles.row} onSubmit={save}>
      <div className={styles.text}>
        <label htmlFor={id} className={styles.key}>
          {nameText[d.name]} URL
        </label>
        <p className={styles.description}>The Neon branch&apos;s DATABASE_URL. Never shown again: only its host.</p>
        <p className={styles.source}>
          {sourceText[d.source]}
          {d.host && `: ${d.host}`}
        </p>
      </div>
      <div className={styles.controls}>
        <input
          id={id}
          className={styles.input}
          type="password"
          autoComplete="off"
          spellCheck={false}
          value={draft}
          placeholder={d.host ? 'set (hidden): type to replace' : 'postgresql://…'}
          onChange={(e) => setDraft(e.target.value)}
        />
        <div className={styles.buttons}>
          <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || !draft.trim()}>
            Save
          </button>
          <button
            type="button"
            className={`ui-strong ${styles.button}`}
            disabled={busy || d.source !== 'page'}
            onClick={() => run(() => api.resetAdminDatabaseUrl(d.name))}
          >
            Clear
          </button>
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
