import type { MouseEvent, ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useIsOwner } from '../features/account/AccountContext'
import { useBackendVersion } from '../lib/backendVersion'
import { useT } from '../i18n/I18n'
import { Footer } from './Footer'
import styles from './Screen.module.css'
import { Tag } from './Tag'

interface TopBarProps {
  /** Shown on the left. Pass `back` for a "← BACK" link instead. */
  title?: string
  /** Where clicking the title goes (e.g. "/" for home). */
  titleLink?: string
  /** Accessible label for the title link. Defaults to `${title}, home`. */
  titleLinkAriaLabel?: string
  /** Where "← BACK" goes when the screen was opened directly. Otherwise it goes one step back in the history. */
  back?: string
  step?: string
  /** Shown on the right instead of the step: a link such as "ACCOUNT". */
  aside?: ReactNode
}

export function TopBar({ title, titleLink, titleLinkAriaLabel, back, step, aside }: TopBarProps) {
  const t = useT()
  const navigate = useNavigate()
  // The first entry of the history has the key 'default': there is nothing to go back to.
  const hasHistory = useLocation().key !== 'default'
  const isOwner = useIsOwner()
  const onDevDatabase = useBackendVersion()?.database === 'dev'
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
        <Link to={back} onClick={goBack} className={styles.back}>
          {t('common.back')}
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
      <div className={styles.version}>
        {isOwner && (
          <>
            v{__APP_VERSION__}
            {onDevDatabase && (
              <Tag tone="dev" title={t('common.devDatabase')}>
                DEV
              </Tag>
            )}
          </>
        )}
      </div>
      {(step || aside) && <div className={styles.step}>{step ?? aside}</div>}
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
      <main className={styles.content}>
        {children}
        <Footer />
      </main>
      {(action || note) && (
        <div className={styles.footer} data-note-above={noteAbove || undefined}>
          {action && <div className={styles.action}>{action}</div>}
          {note && <p className={`caption ${styles.note}`}>{note}</p>}
        </div>
      )}
    </div>
  )
}

interface PrimaryActionProps {
  children: ReactNode
  to?: string
  onClick?: () => void
  disabled?: boolean
  /** The id of the form it submits (the bar sits outside the form), so Enter and password managers work. */
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
