import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import styles from './TextAction.module.css'

type TextActionProps = { children: ReactNode } & (
  | { to: string }
  /** A button instead of a link: "Send the link again", "Try again". */
  | { onClick: () => void; disabled?: boolean }
)

/**
 * Uppercase text link (or button) with a 44px touch target: "Change", "Edit", "Try again".
 * A control whose screen isn't built yet isn't shown at all.
 */
export function TextAction(props: TextActionProps) {
  const className = `ui-strong ${styles.action}`
  if ('to' in props)
    return (
      <Link to={props.to} className={className}>
        {props.children}
      </Link>
    )
  return (
    <button type="button" className={`${className} ${styles.button}`} disabled={props.disabled} onClick={props.onClick}>
      {props.children}
    </button>
  )
}
