import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import styles from './TextAction.module.css'

interface TextActionProps {
  children: ReactNode
  to?: string
  /** A button instead of a link: "Send the link again", "Try again". */
  onClick?: () => void
  disabled?: boolean
}

/**
 * Uppercase text link (or button) with a 44px touch target: "Change", "Edit", "+ Add a stock".
 * Without `to` or `onClick` it's shown but not usable: its screen isn't built yet.
 */
export function TextAction({ children, to, onClick, disabled }: TextActionProps) {
  const className = `ui-strong ${styles.action}`
  if (to)
    return (
      <Link to={to} className={className}>
        {children}
      </Link>
    )
  if (onClick)
    return (
      <button type="button" className={`${className} ${styles.button}`} disabled={disabled} onClick={onClick}>
        {children}
      </button>
    )
  return (
    <span className={className} aria-disabled="true" title="Not available yet">
      {children}
    </span>
  )
}
