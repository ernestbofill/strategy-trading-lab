import { useCallback, useEffect, useState } from 'react'
import { api, InvalidValueError, TooManyTriesError, UnauthorizedError, type AdminPrices, type YahooCheck } from '../../api'
import styles from './AdminSettings.module.css'

const POLL_MS = 3000

const when = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : '–'

/** The daily price refresh: when it ran and runs, a button to run it now, and a check that Yahoo answers. */
export function PricesBox({ onLoggedOut }: { onLoggedOut: () => void }) {
  const [prices, setPrices] = useState<AdminPrices | null>(null)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)
  const [checks, setChecks] = useState<YahooCheck[] | null>(null)
  const [testing, setTesting] = useState(false)

  const load = useCallback(async () => {
    try {
      setPrices(await api.getAdminPrices())
    } catch (e) {
      if (e instanceof UnauthorizedError) onLoggedOut()
    }
  }, [onLoggedOut])

  useEffect(() => {
    let live = true
    api
      .getAdminPrices()
      .then((next) => live && setPrices(next))
      .catch((e: unknown) => e instanceof UnauthorizedError && onLoggedOut())
    return () => {
      live = false
    }
  }, [onLoggedOut])

  // While a refresh runs, look again every few seconds.
  const running = prices?.running ?? false
  useEffect(() => {
    if (!running) return
    const timer = setInterval(() => void load(), POLL_MS)
    return () => clearInterval(timer)
  }, [running, load])

  const refresh = async () => {
    setMessage(null)
    try {
      await api.refreshAdminPrices()
      await load()
    } catch (e) {
      if (e instanceof UnauthorizedError) return onLoggedOut()
      setMessage({ ok: false, text: e instanceof InvalidValueError ? e.message : "Couldn't start it. Try again." })
    }
  }

  const test = async () => {
    setTesting(true)
    setChecks(null)
    setMessage(null)
    try {
      setChecks(await api.testAdminYahoo())
    } catch (e) {
      if (e instanceof UnauthorizedError) return onLoggedOut()
      setMessage({
        ok: false,
        text: e instanceof TooManyTriesError ? 'Too many tests. Try again in an hour.' : "Couldn't run the test. Try again.",
      })
    } finally {
      setTesting(false)
    }
  }

  return (
    <div className={styles.row}>
      <div className={styles.text}>
        <span className={styles.key}>Updates</span>
        <p className={styles.description}>
          Last update: {when(prices?.lastFinished ?? null)} · Next daily run: {when(prices?.nextRun ?? null)} · Newest day stored:{' '}
          {prices?.latestDay ?? '–'}
        </p>
        {prices && prices.lastFinished && (
          <p className={styles.source}>
            Last run: {prices.updated} updated, {prices.fullHistory} in full, {prices.problems.length} failed.
          </p>
        )}
        {prices?.problems.map((p) => (
          <p key={p.name} role="note" className={styles.warning}>
            {p.name}: {p.problem}
          </p>
        ))}
      </div>
      <div className={styles.controls}>
        <div className={styles.buttons}>
          <button type="button" className={`ui-strong ${styles.button}`} disabled={running} onClick={refresh}>
            {running ? 'Refreshing…' : 'Refresh now'}
          </button>
          <button type="button" className={`ui-strong ${styles.button}`} disabled={testing} onClick={test}>
            {testing ? 'Testing…' : 'Test Yahoo'}
          </button>
        </div>
        {checks?.map((c) => (
          <p key={c.symbol} role={c.ok ? 'status' : 'alert'} className={c.ok ? styles.done : styles.error}>
            {c.ok ? `${c.symbol}: ${c.close} on ${c.day} (${c.milliseconds} ms)` : `${c.symbol}: ${c.problem}`}
          </p>
        ))}
        {message && (
          <p role="alert" className={message.ok ? styles.done : styles.error}>
            {message.text}
          </p>
        )}
      </div>
    </div>
  )
}
