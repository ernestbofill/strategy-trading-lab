import { useState } from 'react'
import { api, InvalidValueError, UnauthorizedError, type AdminDatabase, type AdminDatabases, type DatabaseName } from '../../api'
import { refreshBackendVersion } from '../../lib/backendVersion'
import styles from './AdminSettings.module.css'

const nameText: Record<DatabaseName, string> = { dev: 'Dev', production: 'Production' }

interface Props {
  databases: AdminDatabases
  onChanged: () => Promise<void>
  onLoggedOut: () => void
}

/**
 * Which database the API uses (dev or production). Their URLs are set only on the server, so this box can switch
 * between them but never point the API anywhere else. The API tests a switch first.
 */
export function DatabaseBox({ databases, onChanged, onLoggedOut }: Props) {
  const { active, home } = databases
  const other: DatabaseName = active === 'dev' ? 'production' : 'dev'
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const switchTo = async () => {
    setBusy(true)
    setError(null)
    try {
      await api.switchAdminDatabase(other)
      await Promise.all([onChanged(), refreshBackendVersion()])
    } catch (e) {
      if (e instanceof UnauthorizedError) return onLoggedOut()
      setError(e instanceof InvalidValueError ? e.message : "Couldn't switch. Try again.")
    } finally {
      setBusy(false)
    }
  }

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
      {databases.databases.map((d) => (
        <UrlRow key={d.name} database={d} />
      ))}
    </section>
  )
}

/** Where a database's URL comes from on the server, and its host. The URL holds a password, so nothing more. */
function UrlRow({ database: d }: { database: AdminDatabase }) {
  return (
    <div className={styles.row}>
      <div className={styles.text}>
        <span className={styles.key}>{nameText[d.name]}</span>
        <p className={styles.description}>{d.host ?? 'Not set'}</p>
        <p className={styles.source}>{d.variable} on the server</p>
      </div>
    </div>
  )
}
