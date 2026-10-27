import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../api'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { invalidate } from '../../lib/useLoadable'
import { dashboardCacheKey } from '../dashboard/useDashboard'
import { dashboardPath } from '../new-strategy/paths'
import styles from './DeleteStrategy.module.css'

/** "Delete the strategy", after a confirm step: it goes for good, with its signals, trades and results. */
export function DeleteStrategy({ id, name }: { id: string; name: string }) {
  const t = useT()
  const navigate = useNavigate()
  const [asking, setAsking] = useState(false)
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  if (!asking)
    return (
      <div className={styles.start}>
        <TextAction onClick={() => setAsking(true)}>{t('strategy.delete')}</TextAction>
      </div>
    )

  const remove = async () => {
    setBusy(true)
    setFailed(false)
    try {
      await api.deleteStrategy(id)
    } catch {
      setFailed(true)
      setBusy(false)
      return
    }
    invalidate(dashboardCacheKey)
    // The strategy page is gone: going back must not open it.
    navigate(dashboardPath, { replace: true })
  }

  return (
    <div className={styles.confirmBox}>
      <p>{t('strategy.deleteAsk', { name })}</p>
      {failed && <p role="alert">{t('strategy.deleteFailed')}</p>}
      <div className={styles.confirmActions}>
        <button type="button" className={styles.btnDanger} disabled={busy} onClick={remove}>
          {busy ? t('strategy.deleting') : t('strategy.deleteConfirm')}
        </button>
        <button type="button" className={styles.btnSecondary} disabled={busy} onClick={() => setAsking(false)}>
          {t('strategy.deleteKeep')}
        </button>
      </div>
    </div>
  )
}
