import type { ReactNode } from 'react'
import styles from './Columns.module.css'

/** One column on phones; main + aside side by side from 1024px. */
export function Columns({ main, aside }: { main: ReactNode; aside: ReactNode }) {
  return (
    <div className={styles.columns}>
      <div className={styles.column}>{main}</div>
      <div className={styles.column}>{aside}</div>
    </div>
  )
}
