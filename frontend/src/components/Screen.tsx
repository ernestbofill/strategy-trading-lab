import type { MouseEvent, ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useRole } from '../features/account/AccountContext'
import type { Role } from '../api'
import { useT } from '../i18n/I18n'
import type { MessageKey } from '../i18n/messages/en'
import { Footer } from './Footer'
import styles from './Screen.module.css'
import { useHasHistory, type UpState } from './useLeave'

/** The name of the parent `back` leads to, for the up link of a screen opened directly. `role` is undefined while the account loads. */
function upLabel(to: string, role: Role | null | undefined): MessageKey {
  if (to === '/') return role === undefined ? 'common.back' : role === null ? 'common.up.home' : 'common.up.dashboard'
  if (to === '/admin') return 'common.up.admin'
  if (/^\/strategies\/[^/]+$/.test(to)) return 'common.up.strategy'
  return 'common.back'
}

interface TopBarProps {
  /** Shown on the left. Pass `back` for a "← BACK" link instead. */
  title?: string
  /** Where clicking the title goes (e.g. "/" for home). */
  titleLink?: string
  /** Accessible label for the title link. Defaults to `${title}, home`. */
  titleLinkAriaLabel?: string
  /**
   * The screen's parent. Opened from inside the app, "← BACK" goes one step back in the history.
   * Opened directly (a notification, an email link), the link goes up to this parent and names it
   * ("← STRATEGY", "← DASHBOARD") where it can.
   */
  back?: string
  step?: string
  /** Shown on the right instead of the step: a link such as "ACCOUNT". */
  aside?: ReactNode
}

export function TopBar({ title, titleLink, titleLinkAriaLabel, back, step, aside }: TopBarProps) {
  const t = useT()
  const navigate = useNavigate()
  // Opened directly, or reached by going up, it keeps going up.
  const hasHistory = useHasHistory()
  const role = useRole()
  const goBack = (e: MouseEvent<HTMLAnchorElement>) => {
    const plainClick = e.button === 0 && !(e.metaKey || e.ctrlKey || e.shiftKey || e.altKey)
    if (hasHistory && plainClick) {
      e.preventDefault()
      navigate(-1)
    }
  }

  return (
    <header className={styles.topBar}>
      {back ? (
        // Going up replaces the entry, so the parent's own back can't return here.
        <Link to={back} onClick={goBack} replace state={{ up: true } satisfies UpState} className={styles.back}>
          {t(hasHistory ? 'common.back' : upLabel(back, role))}
        </Link>
      ) : titleLink ? (
        <Link
          to={titleLink}
          className={styles.titleLink}
          aria-label={titleLinkAriaLabel ?? (title ? `${title}, home` : undefined)}
        >
          {title}
        </Link>
      ) : (
        <div>{title}</div>
      )}
      {(step || aside) && <div className={styles.step}>{step ?? aside}</div>}
    </header>
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
