import type { ReactNode } from 'react'
import styles from './Stats.module.css'

/** A row of small figure tiles (budget used, signals, invested): one per row on a phone, side by side on desktop. */
export function Stats({ children }: { children: ReactNode }) {
  return <dl className={styles.stats}>{children}</dl>
}

/** One tile: a short label above a mono value. */
export function Stat({ label, children, tone }: { label: ReactNode; children: ReactNode; tone?: 'up' | 'down' }) {
  return (
    <div className={styles.stat}>
      <dt className={styles.label}>{label}</dt>
      <dd className={styles.value} data-tone={tone}>
        {children}
      </dd>
    </div>
  )
}
