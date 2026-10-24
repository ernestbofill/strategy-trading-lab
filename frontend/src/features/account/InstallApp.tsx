import { useState, useSyncExternalStore } from 'react'
import { FormMessage } from '../../components/Field'
import { consumePwaInstallPrompt, getPwaInstallState, subscribePwaInstall } from '../../lib/pwaInstall'
import styles from './Account.module.css'

const manualInstallInstructions =
  'On iPhone or iPad, open this page in Safari, tap Share, then Add to Home Screen. On Android, open the browser menu and choose Install app or Add to Home screen.'

export function InstallApp() {
  const { prompt, installed } = useSyncExternalStore(subscribePwaInstall, getPwaInstallState, getPwaInstallState)
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const install = async () => {
    if (busy) return
    setStatus(null)
    setError(null)

    if (installed) {
      setStatus('The app is already installed on this device.')
      return
    }

    if (!prompt) {
      setStatus(manualInstallInstructions)
      return
    }

    consumePwaInstallPrompt(prompt)
    setBusy(true)
    try {
      await prompt.prompt()
      const { outcome } = await prompt.userChoice
      if (outcome === 'accepted') setStatus('Installation started.')
      else setStatus(manualInstallInstructions)
    } catch {
      setError('Could not open the install prompt. Use your browser menu instead.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      {!installed && (
        <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={() => void install()}>
          {busy ? 'Opening install prompt…' : 'Install the app'}
        </button>
      )}
      {installed && <FormMessage tone="done">The app is installed on this device.</FormMessage>}
      {status && <FormMessage tone="done">{status}</FormMessage>}
      {error && <FormMessage tone="error">{error}</FormMessage>}
    </>
  )
}
