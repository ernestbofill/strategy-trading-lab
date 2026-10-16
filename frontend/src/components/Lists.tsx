import type { ReactNode } from 'react'
import styles from './Lists.module.css'

/** ▸ bullet list. */
export function BulletList({ items }: { items: ReactNode[] }) {
  return (
    <ul className={styles.bullets}>
      {items.map((item, i) => (
        <li key={i}>{item}</li>
      ))}
    </ul>
  )
}

/** Label on the left, mono value on the right, in a deep well. */
export function KeyValueList({ rows }: { rows: [label: string, value: ReactNode][] }) {
  return (
    <dl className={`well ${styles.kv}`}>
      {rows.map(([label, value]) => (
        <div key={label} className={styles.kvRow}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  )
}
