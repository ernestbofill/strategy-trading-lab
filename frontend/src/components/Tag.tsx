import type { ReactNode } from 'react'
import styles from './Tag.module.css'

export type TagTone = 'sim' | 'live' | 'ai'

/** Outlined mono label: SIM, LIVE, AI. Always carries a word, never colour alone. */
export function Tag({ tone, children }: { tone: TagTone; children: ReactNode }) {
  return (
    <span className={styles.tag} data-tone={tone}>
      {children}
    </span>
  )
}
