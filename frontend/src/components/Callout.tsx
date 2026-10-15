import type { ReactNode } from 'react'
import styles from './Callout.module.css'

/** Info or warning box. The title is a word, so the tone never relies on colour alone. */
export function Callout({ tone, title, children }: { tone: 'info' | 'warning'; title?: string; children: ReactNode }) {
  return (
    <div className={styles.callout} data-tone={tone}>
      {title && <div className="ui-strong">{title}</div>}
      {children}
    </div>
  )
}

/** A bold lead-in followed by text: "What can go wrong: …", "Not tested: …". */
export function LabelledLine({ label, tone, children }: { label: string; tone?: 'warning' | 'muted'; children: ReactNode }) {
  return (
    <p className={styles.line}>
      <b className={styles.lineLabel} data-tone={tone}>
        {label}
      </b>{' '}
      {children}
    </p>
  )
}
