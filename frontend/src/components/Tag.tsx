import type { ReactNode } from 'react'
import styles from './Tag.module.css'

export type TagTone = 'sim' | 'live' | 'ai' | 'dev' | 'ended'

/** Outlined mono label: SIM, LIVE, AI, DEV, ENDED. Always carries a word, never colour alone. */
export function Tag({ tone, title, children }: { tone: TagTone; title?: string; children: ReactNode }) {
  return (
    <span className={styles.tag} data-tone={tone} title={title}>
      {children}
    </span>
  )
}
