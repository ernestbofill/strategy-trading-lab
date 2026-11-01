import { useEffect, useRef, useState } from 'react'
import { api, type AdminAlert, type AlertSource } from '../../api'
import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { isoDate } from '../../lib/format'
import styles from './AdminAlerts.module.css'

type State = { status: 'loading' } | { status: 'error' } | { status: 'ready'; alerts: AdminAlert[] }

const sourceText: Record<AlertSource, string> = {
  dailyCheck: 'Daily check',
  prices: 'Prices',
  strategies: 'Strategy',
  aiStrategies: 'AI strategy',
  email: 'Email',
}

/** "2026-10-30 08:12" in the owner's time zone. */
const at = (iso: string) => {
  const d = new Date(iso)
  return `${isoDate(d)} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function fetchAlerts(): Promise<State> {
  try {
    return { status: 'ready', alerts: await api.getAdminAlerts() }
  } catch {
    return { status: 'error' }
  }
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
  const [state, setState] = useState<State>({ status: 'loading' })
  const [failed, setFailed] = useState(false)
  const [busy, setBusy] = useState<string | null>(null)

  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    void fetchAlerts().then((next) => mounted.current && setState(next))
    return () => {
      mounted.current = false
    }
  }, [])

  const dismiss = async (id: string) => {
    setBusy(id)
    setFailed(false)
    let refused = false
    try {
      await api.dismissAdminAlert(id)
    } catch {
      refused = true
    }
    const next = await fetchAlerts()
    if (!mounted.current) return
    // A failed reload keeps the list on screen; a refusal is only a failure if the alert is still open (a double tap, or a
    // run that cleared it meanwhile, answers 404).
    if (next.status === 'ready') setState(next)
    setFailed(next.status === 'ready' ? refused && next.alerts.some((a) => a.id === id && a.status === 'open') : true)
    setBusy(null)
  }

  const of = (status: AdminAlert['status']) => (state.status === 'ready' ? state.alerts.filter((a) => a.status === status) : [])
  const open = of('open')
  const dismissed = of('dismissed')
  const cleared = of('cleared')

  return (
    <Screen top={<TopBar back="/admin" step="ADMIN" />}>
      <PageHeader title="Alerts" lead="Problems the daily jobs found. Each clears itself once a later run finds it fixed." />
      {state.status === 'loading' && <ScreenMessage>Loading the alerts…</ScreenMessage>}
      {state.status === 'error' && <ScreenMessage>Could not load the alerts. Try again later.</ScreenMessage>}
      {state.status === 'ready' && (
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
      )}
    </Screen>
  )
}
