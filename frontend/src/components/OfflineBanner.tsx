import { useSyncExternalStore } from 'react'
import { useT } from '../i18n/I18n'
import styles from './OfflineBanner.module.css'

const subscribe = (changed: () => void) => {
  window.addEventListener('online', changed)
  window.addEventListener('offline', changed)
  return () => {
    window.removeEventListener('online', changed)
    window.removeEventListener('offline', changed)
  }
}

/** A line on top of every screen while the device is offline: the PWA still shows what it kept, which may be old. */
export function OfflineBanner() {
  const t = useT()
  const online = useSyncExternalStore(subscribe, () => navigator.onLine, () => true)
  if (online) return null
  return (
    <p className={styles.banner} role="status">
      {t('errors.offline')}
    </p>
  )
}
