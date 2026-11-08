import { Component, type ErrorInfo, type ReactNode } from 'react'
import { useT } from '../i18n/I18n'
import styles from './ErrorBoundary.module.css'

/**
 * Catches a screen that crashes while rendering, so the user gets a way out instead of a blank page. Opening another page
 * (`resetKey`, the path) clears the crash. Never key it by the path instead: navigations run as transitions, and remounting
 * the whole tree in one left the old screen on the page above the new one (#196). The fallback stays plain (no footer, no
 * account): it must not crash too.
 */
type Props = { resetKey: string; children: ReactNode }

export class ErrorBoundary extends Component<Props, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  // After the update, not in getDerivedStateFromProps: a page that crashes as it opens would have its crash wiped there
  // (React retries the render with the new path) and the error would take the whole app down.
  componentDidUpdate(previous: Props) {
    // oxlint-disable-next-line react/no-did-update-set-state -- one guarded reset when the path changes, as react-error-boundary does
    if (this.state.failed && previous.resetKey !== this.props.resetKey) this.setState({ failed: false })
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('A screen crashed', error, info.componentStack)
  }

  render() {
    return this.state.failed ? <CrashScreen /> : this.props.children
  }
}

function CrashScreen() {
  const t = useT()
  return (
    <main className={styles.crash} role="alert">
      <h1 className={styles.title}>{t('errors.crash.title')}</h1>
      <p className={styles.line}>{t('errors.crash.line')}</p>
      <button type="button" className="btn-primary ui-strong" onClick={() => window.location.reload()}>
        {t('errors.crash.reload')}
      </button>
      <a className={styles.home} href="/">
        {t('errors.goHome')}
      </a>
    </main>
  )
}
