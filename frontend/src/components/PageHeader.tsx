import type { ReactNode } from 'react'
import styles from './PageHeader.module.css'
import { Tagline } from './Tagline'

interface PageHeaderProps {
  /** Small line above the title; may hold a Tag. */
  eyebrow?: ReactNode
  title: ReactNode
  /** Template tagline, shown in quotes under the title. */
  tagline?: string
  lead?: ReactNode
}

export function PageHeader({ eyebrow, title, tagline, lead }: PageHeaderProps) {
  return (
    <div className={styles.header}>
      {eyebrow && <div className={styles.eyebrow}>{eyebrow}</div>}
      <h1 className={styles.title}>{title}</h1>
      {tagline && <Tagline text={tagline} />}
      {lead && <p className={styles.lead}>{lead}</p>}
    </div>
  )
}
