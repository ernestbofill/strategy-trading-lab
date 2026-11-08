import { Component, type ErrorInfo, type ReactNode } from 'react'
import { useT } from '../i18n/I18n'
import styles from './ErrorBoundary.module.css'

/**
 * Catches a screen that crashes while rendering, so the user gets a way out instead of a blank page. Keyed by the path where
 * it is used, so opening another page starts clean. The fallback stays plain (no footer, no account): it must not crash too.
 */
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
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
