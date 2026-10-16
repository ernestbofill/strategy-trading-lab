import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import styles from './NavRow.module.css'

interface NavRowProps {
  label: ReactNode
  /** Short mono summary after the label: "· 8 ticked". */
  meta?: ReactNode
  to?: string
  /** Use instead of `to` for a row that expands content in place. */
  onClick?: () => void
  expanded?: boolean
}

/** Full-width row that leads somewhere, with a › on the right. */
export function NavRow({ label, meta, to, onClick, expanded }: NavRowProps) {
  const content = (
    <>
      <span>
        {label}
        {meta && <span className={styles.meta}> · {meta}</span>}
      </span>
      <span className={styles.chevron} data-expanded={expanded || undefined} aria-hidden="true">
        ›
      </span>
    </>
  )
  if (to) {
    return (
      <Link to={to} className={styles.row}>
        {content}
      </Link>
    )
  }
  return (
    <button type="button" onClick={onClick} aria-expanded={expanded} className={styles.row}>
      {content}
    </button>
  )
}
