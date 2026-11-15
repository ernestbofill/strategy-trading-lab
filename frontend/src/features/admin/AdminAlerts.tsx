import { useState } from 'react'
import { api, type AdminAlert, type AlertSource } from '../../api'
import { PageHeader } from '../../components/PageHeader'
import { Loaded } from '../../components/Loaded'
import { Screen } from '../../components/Screen'
import { AdminTopBar } from './AdminTopBar'
import { adminCacheKeys } from './cacheKeys'
import { Section } from '../../components/Section'
import { localDateTime as at } from '../../lib/format'
import { reload, useLoadable } from '../../lib/useLoadable'
import styles from './AdminAlerts.module.css'


const sourceText: Record<AlertSource, string> = {
  dailyCheck: 'Daily check',
  prices: 'Prices',
  strategies: 'Strategy',
  aiStrategies: 'AI strategy',
  email: 'Email',
}


function AlertCard({ alert, onDismiss, busy }: { alert: AdminAlert; onDismiss?: () => void; busy?: boolean }) {
  return (
    <li className={`well ${styles.alert}`} data-status={alert.status}>
      <div className={styles.head}>
        <h3 className={styles.title}>{alert.title}</h3>
        <span className={styles.source}>{sourceText[alert.source]}</span>
      </div>
      {alert.detail && <p className={styles.detail}>{alert.detail}</p>}
      <p className={styles.meta}>
        {alert.status === 'cleared' && alert.closedAt
          ? `Cleared ${at(alert.closedAt)} · last seen ${at(alert.lastSeenAt)}`
          : `Since ${at(alert.firstSeenAt)} · last seen ${at(alert.lastSeenAt)} · ${alert.count}×`}
      </p>
      {onDismiss && (
        <button type="button" className={styles.dismiss} onClick={onDismiss} disabled={busy}>
          Dismiss
        </button>
      )}
    </li>
  )
}

/**
 * Admin alerts (/admin/alerts): what the background jobs found wrong (a failed daily check or price fetch, a strategy missing
 * data, emails that didn't go out), instead of emails to the owner. Alerts clear themselves once a later run finds them fixed.
 */
export function AdminAlerts() {
  const state = useLoadable(adminCacheKeys.alerts, api.getAdminAlerts)
  // The alert a dismiss was refused for: only a failure while it is still open (a double tap, or a run that cleared it
  // meanwhile, answers 404). A failed reload keeps the list on screen, and says so.
  const [refused, setRefused] = useState<string | null>(null)
  const [reloadFailed, setReloadFailed] = useState(false)
  const [busy, setBusy] = useState<string | null>(null)

  const dismiss = async (id: string) => {
    setBusy(id)
    setRefused(null)
    setReloadFailed(false)
    try {
      await api.dismissAdminAlert(id)
    } catch {
      setRefused(id)
    }
    setReloadFailed(!(await reload(adminCacheKeys.alerts)))
    setBusy(null)
  }

  return (
    <Screen top={<AdminTopBar />}>
      <PageHeader title="Alerts" lead="Problems the daily jobs found. Each clears itself once a later run finds it fixed." />
      <Loaded state={state} what="the alerts">
        {(alerts) => {
          const of = (status: AdminAlert['status']) => alerts.filter((a) => a.status === status)
          const open = of('open')
          const dismissed = of('dismissed')
          const cleared = of('cleared')
          const failed = reloadFailed || open.some((a) => a.id === refused)
          return (
            <>
              {failed && <p className={styles.error} role="alert">Could not dismiss it, or reload the list. Try again.</p>}
              <Section title={`Open · ${open.length}`}>
                {open.length === 0 ? (
                  <p className={styles.muted}>Nothing open.</p>
                ) : (
                  <ul className={styles.list}>
                    {open.map((a) => (
                      <AlertCard key={a.id} alert={a} busy={busy === a.id} onDismiss={() => void dismiss(a.id)} />
                    ))}
                  </ul>
                )}
              </Section>
              {dismissed.length > 0 && (
                <Section title={`Dismissed · ${dismissed.length}`}>
                  <p className={styles.muted}>Quiet while the problem lasts. Back only if it clears and happens again.</p>
                  <ul className={styles.list}>
                    {dismissed.map((a) => (
                      <AlertCard key={a.id} alert={a} />
                    ))}
                  </ul>
                </Section>
              )}
              {cleared.length > 0 && (
                <Section title="Cleared this week">
                  <ul className={styles.list}>
                    {cleared.map((a) => (
                      <AlertCard key={a.id} alert={a} />
                    ))}
                  </ul>
                </Section>
              )}
            </>
          )
        }}
      </Loaded>
    </Screen>
  )
}
