import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import styles from './Screen.module.css'

interface TopBarProps {
  /** Shown on the left. Pass `back` for a "← BACK" link instead. */
  title?: string
  back?: string
  step?: string
}

export function TopBar({ title, back, step }: TopBarProps) {
  return (
    <header className={styles.topBar}>
      {back ? (
        <Link to={back} className={styles.back}>
          ← BACK
        </Link>
      ) : (
        <div>{title}</div>
      )}
      {step && <div>{step}</div>}
    </header>
  )
}

interface ScreenProps {
  top: ReactNode
  /** The one primary action, in the bottom bar. */
  action?: ReactNode
  /** Small line in the bottom bar: a hint or the disclaimer. Left of the action on wide screens. */
  note?: ReactNode
  /** On phones the note sits under the action unless this is set. */
  noteAbove?: boolean
  children: ReactNode
}

/** Mobile-first screen shell: top bar, scrolling content and a bottom bar for the one primary action. */
export function Screen({ top, action, note, noteAbove, children }: ScreenProps) {
  return (
    <div className={styles.screen}>
      {top}
      <main className={styles.content}>{children}</main>
      {(action || note) && (
        <footer className={styles.footer} data-note-above={noteAbove || undefined}>
          {action && <div className={styles.action}>{action}</div>}
          {note && <p className={`caption ${styles.note}`}>{note}</p>}
        </footer>
      )}
    </div>
  )
}

interface PrimaryActionProps {
  children: ReactNode
  to?: string
  onClick?: () => void
  disabled?: boolean
}

/** The screen's primary button: a link when it navigates, a button when it acts. */
export function PrimaryAction({ children, to, onClick, disabled }: PrimaryActionProps) {
  const className = 'btn-primary ui-strong'
  if (disabled) {
    return (
      <span className={className} aria-disabled="true">
        {children}
      </span>
    )
  }
  if (to) {
    return (
      <Link to={to} onClick={onClick} className={className}>
        {children}
      </Link>
    )
  }
  return (
    <button type="button" onClick={onClick} className={className}>
      {children}
    </button>
  )
}

/** Loading, error and not-found text inside a screen. */
export function ScreenMessage({ children }: { children: ReactNode }) {
  return <p className={styles.message}>{children}</p>
}
