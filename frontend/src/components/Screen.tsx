import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useRole } from '../features/account/AccountContext'
import { logInPathFor } from '../features/account/paths'
import { accountPath } from '../features/dashboard/paths'
import { homePath } from '../features/new-strategy/paths'
import { useT } from '../i18n/I18n'
import type { MessageKey } from '../i18n/messages/en'
import { Footer } from './Footer'
import styles from './Screen.module.css'
import { TextAction } from './TextAction'

interface TopBarProps {
  /** The left side as text: the screen's name ("STL // LOG IN"). Such a screen gets no right side unless it passes one. */
  title?: string
  /** Makes the title a link. */
  titleLink?: string
  /** The left side as a direct link home, no arrow: "DASHBOARD", or "WELCOME" for a guest. */
  home?: boolean
  /** The left side as "← [upLabel]": one level up, always to this parent, never back in the history. */
  up?: string
  /** The up link's name. "BACK" by default: the new-strategy steps, whose parent is the step before. */
  upLabel?: MessageKey
  /** The right side as a label: "2 / 5", the strategy's name. */
  step?: string
  /** The right side as links or a label. Left out, it is "ACCOUNT" (signed in) or "LOG IN" (guest); `null` for nothing. */
  aside?: ReactNode
}

/**
 * The one-line bar above every screen. Left: where the user is, a direct link home, or one level up.
 * Right: one label or one or two links.
 */
export function TopBar({ title, titleLink, home, up, upLabel = 'common.back', step, aside }: TopBarProps) {
  const t = useT()
  const role = useRole()
  const plainTitle = title !== undefined && !titleLink && !home && !up
  const right = step ?? (aside === undefined && !plainTitle ? <UserLink role={role} /> : aside)

  return (
    <header className={styles.topBar}>
      {up ? (
        <Link to={up} className={styles.left}>
          <UpArrow />
          <span className={styles.label}>{t(upLabel)}</span>
        </Link>
      ) : home ? (
        // "STL" while the account loads, so a guest never sees DASHBOARD flash.
        <Link to={homePath} className={styles.left}>
          <span className={styles.label}>{role === undefined ? 'STL' : t(role ? 'common.home.dashboard' : 'common.home.welcome')}</span>
        </Link>
      ) : titleLink ? (
        <Link to={titleLink} className={styles.left}>
          <span className={styles.label}>{title}</span>
        </Link>
      ) : (
        <div>{title}</div>
      )}
      {right && <div className={styles.right}>{right}</div>}
    </header>
  )
}

/** The default right side: the account, or log in (coming back here) for a guest. Nothing while the account loads. */
function UserLink({ role }: { role: ReturnType<typeof useRole> }) {
  const t = useT()
  const { pathname } = useLocation()
  if (role === undefined) return null
  return role ? (
    <TextAction to={accountPath}>{t('common.account')}</TextAction>
  ) : (
    <TextAction to={logInPathFor(pathname)}>{t('common.logIn')}</TextAction>
  )
}

/**
 * Drawn, not the "←" character: iOS draws that glyph from a fallback font that sits low on the line.
 * Centred on the text by the link's flex box.
 */
function UpArrow() {
  return (
    <svg className={styles.arrow} width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M11 6H1.5M5.5 2 1.5 6l4 4" />
    </svg>
  )
}

interface ScreenProps {
  top: ReactNode
  /** The one primary action, at the end of the content. */
  action?: ReactNode
  /** Small line next to the action: a hint, or the full disclaimer where the user commits. Left of the action on wide screens. */
  note?: ReactNode
  /** On phones the note sits under the action unless this is set. */
  noteAbove?: boolean
  children: ReactNode
}

/** Mobile-first screen shell: top bar, then the content, ending with the one primary action and the site footer. */
export function Screen({ top, action, note, noteAbove, children }: ScreenProps) {
  return (
    <div className={styles.screen}>
      {top}
      <main className={styles.content}>
        {children}
        {(action || note) && (
          <div className={styles.actions} data-note-above={noteAbove || undefined}>
            {action && <div className={styles.action}>{action}</div>}
            {note && <p className={`caption ${styles.note}`}>{note}</p>}
          </div>
        )}
        <Footer />
      </main>
    </div>
  )
}

interface PrimaryActionProps {
  children: ReactNode
  to?: string
  onClick?: () => void
  disabled?: boolean
  /** The id of the form it submits (the action sits outside the form), so Enter and password managers work. */
  form?: string
}

/** The screen's primary button: a link when it navigates, a button when it acts, a submit button for a form. */
export function PrimaryAction({ children, to, onClick, disabled, form }: PrimaryActionProps) {
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
  if (form) {
    return (
      <button type="submit" form={form} className={className}>
        {children}
      </button>
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
