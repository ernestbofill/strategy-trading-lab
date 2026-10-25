import { useState, useSyncExternalStore } from 'react'
import { FormMessage } from '../../components/Field'
import { useT } from '../../i18n/I18n'
import { consumePwaInstallPrompt, getPwaInstallState, subscribePwaInstall } from '../../lib/pwaInstall'
import styles from './Account.module.css'

export function InstallApp() {
  const t = useT()
  const { prompt, installed } = useSyncExternalStore(subscribePwaInstall, getPwaInstallState, getPwaInstallState)
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (installed) return <FormMessage tone="done">{t('account.install.installed')}</FormMessage>

  const install = async () => {
    if (busy) return
    setStatus(null)
    setError(null)

    if (!prompt) {
      setStatus(t('account.install.manual'))
      return
    }

    consumePwaInstallPrompt(prompt)
    setBusy(true)
    try {
      await prompt.prompt()
      const { outcome } = await prompt.userChoice
      if (outcome === 'accepted') setStatus(t('account.install.started'))
      else setStatus(t('account.install.manual'))
    } catch {
      setError(t('account.install.failed'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={() => void install()}>
        {busy ? t('account.install.opening') : t('account.install.button')}
      </button>
      {status && <FormMessage tone="done">{status}</FormMessage>}
      {error && <FormMessage tone="error">{error}</FormMessage>}
    </>
  )
}
