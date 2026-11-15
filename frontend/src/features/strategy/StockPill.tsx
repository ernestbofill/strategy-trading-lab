import type { PillTone } from './stockLine'
import styles from './StockPill.module.css'

/** What became of a signal, on Signals and trades: done ("DID") or skipped; the others share the stock tones. */
export type SignalTone = Exclude<PillTone, 'watching' | 'muted'> | 'done' | 'skipped'

/**
 * A stock's status under the rules ("CANDIDATE", "NOT A CANDIDATE", "WAITING FOR YOU"), on the strategy and stock pages, or
 * a signal's on Signals and trades.
 */
export function StockPill({ tone, children }: { tone: PillTone | SignalTone; children: string }) {
  return (
    <span className={styles.pill} data-tone={tone}>
      {children}
    </span>
  )
}
