import { useEffect, useState } from 'react'
import { api, InvalidValueError, TooManyTriesError, UnauthorizedError, type AdminPrices, type YahooCheck } from '../../api'
import { localDateTime } from '../../lib/format'
import { reload, useLoadable } from '../../lib/useLoadable'
import styles from './AdminSettings.module.css'
import { adminCacheKeys } from './cacheKeys'

const POLL_MS = 3000

const when = (iso: string | null) => (iso ? localDateTime(iso) : '–')

/** The daily price refresh: when it ran and runs, a button to run it now, and a check that Yahoo answers. */
export function PricesBox() {
  const state = useLoadable(adminCacheKeys.prices, api.getAdminPrices)
  // Until it loads (or when it can't: the session ended and the app shows Log-in), the box shows dashes.
  const prices: AdminPrices | null = state.status === 'ready' ? state.data : null
  const [message, setMessage] = useState<string | null>(null)
  const [checks, setChecks] = useState<YahooCheck[] | null>(null)
  const [testing, setTesting] = useState(false)

  // While a refresh runs, look again every few seconds.
  const running = prices?.running ?? false
  useEffect(() => {
    if (!running) return
    const timer = setInterval(() => void reload(adminCacheKeys.prices), POLL_MS)
    return () => clearInterval(timer)
  }, [running])

  const refresh = async () => {
    setMessage(null)
    try {
      await api.refreshAdminPrices()
      await reload(adminCacheKeys.prices, { fresh: true })
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setMessage(e instanceof InvalidValueError ? e.message : "Couldn't start it. Try again.")
    }
  }

  const test = async () => {
    setTesting(true)
    setChecks(null)
    setMessage(null)
    try {
      setChecks(await api.testAdminYahoo())
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setMessage(e instanceof TooManyTriesError ? 'Too many tests. Try again in an hour.' : "Couldn't run the test. Try again.")
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
            Last run: {prices.updated} updated, {prices.fullHistory} in full, {prices.problems.length} failed
            {prices.newSignals === null
              ? '. Strategies not evaluated.'
              : `; ${prices.newSignals} new ${prices.newSignals === 1 ? 'signal' : 'signals'}${prices.evaluationFailures > 0 ? `, ${prices.evaluationFailures} ${prices.evaluationFailures === 1 ? 'strategy' : 'strategies'} failed` : ''}.`}
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
          <p role="alert" className={styles.error}>
            {message}
          </p>
        )}
      </div>
    </div>
  )
}
