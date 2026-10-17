import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import styles from './TextAction.module.css'

/**
 * Uppercase text link with a 44px touch target: "Change", "Edit", "+ Add a stock".
 * Without `to` it's shown but not usable: its screen isn't built yet.
 */
export function TextAction({ children, to, onClick }: { children: ReactNode; to?: string; onClick?: () => void }) {
  const className = `ui-strong ${styles.action}`
  return to ? (
    <Link to={to} onClick={onClick} className={className}>
      {children}
    </Link>
  ) : (
    <span className={className} aria-disabled="true" title="Not available yet">
      {children}
    </span>
  )
}
