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

/** Mobile-first screen shell: top bar, scrolling content and a bottom bar for the one primary action. */
export function Screen({ top, footer, children }: { top: ReactNode; footer?: ReactNode; children: ReactNode }) {
  return (
    <div className={styles.screen}>
      {top}
      <main className={styles.content}>{children}</main>
      {footer && <footer className={styles.footer}>{footer}</footer>}
    </div>
  )
}
