import { useId, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useT } from '../i18n/I18n'
import styles from './LinkList.module.css'

interface LinkSectionProps {
  title: string
  /** A count badge after the title; `countLabel` says what it counts to a screen reader (" waiting"; else " in this list"). */
  count?: number
  countLabel?: string
  children: ReactNode
}

/** A titled group of a screen, named by its heading, with an optional count badge (Waiting for you, What changed). */
export function LinkSection({ title, count, countLabel, children }: LinkSectionProps) {
  const t = useT()
  const id = useId()
  return (
    <section className={styles.section} aria-labelledby={id}>
      <div className={styles.head}>
        <h2 id={id} className={`ui-strong ${styles.title}`}>
          {title}
        </h2>
        {count !== undefined && (
          <span className={styles.count}>
            {count}
            <span className="visually-hidden">{countLabel ?? t('common.countHidden')}</span>
          </span>
        )}
      </div>
      {children}
    </section>
  )
}

export interface LinkItem {
  key: string
  to: string
  /** The tag at the start: the mode, or AI. */
  tag: ReactNode
  children: ReactNode
}

/** Rows that each lead to a screen, with a › on the right, in one framed list. */
export function LinkList({ items }: { items: LinkItem[] }) {
  return (
    <ul className={styles.list}>
      {items.map((item) => (
        <li key={item.key}>
          <Link to={item.to} className={styles.row}>
            {item.tag}
            <span className={styles.content}>{item.children}</span>
            <span className={styles.chevron} aria-hidden="true">
              ›
            </span>
          </Link>
        </li>
      ))}
    </ul>
  )
}
