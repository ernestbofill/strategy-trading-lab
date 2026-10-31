import type { PillTone } from './stockLine'
import styles from './StockPill.module.css'

/** A stock's status under the rules ("CANDIDATE", "NOT A CANDIDATE", "WAITING FOR YOU"), on the strategy and stock pages. */
export function StockPill({ tone, children }: { tone: PillTone; children: string }) {
  return (
    <span className={styles.pill} data-tone={tone}>
      {children}
    </span>
  )
}
