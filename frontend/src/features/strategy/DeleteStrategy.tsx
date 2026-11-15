import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../api'
import { ConfirmBox } from '../../components/ConfirmBox'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { invalidateAll } from '../../lib/useLoadable'
import { dashboardPath } from '../new-strategy/paths'
import styles from './DeleteStrategy.module.css'

/** "Delete the strategy", after a confirm step: it goes for good, with its signals, trades and results. */
export function DeleteStrategy({ id, name }: { id: string; name: string }) {
  const t = useT()
  const navigate = useNavigate()
  const [asking, setAsking] = useState(false)

  if (!asking)
    return (
      <div className={styles.start}>
        <TextAction onClick={() => setAsking(true)}>{t('strategy.delete')}</TextAction>
      </div>
    )

  const remove = async () => {
    await api.deleteStrategy(id)
    // The strategy page is gone: going back must not open it.
    navigate(dashboardPath, { replace: true })
    // Its page, rounds and signals may be cached, and the dashboard still lists it. After leaving the page, so it doesn't
    // load the strategy again.
    invalidateAll()
  }

  return (
    <ConfirmBox
      ask={t('strategy.deleteAsk', { name })}
      confirmLabel={t('strategy.deleteConfirm')}
      busyLabel={t('strategy.deleting')}
      keepLabel={t('strategy.deleteKeep')}
      failedText={t('strategy.deleteFailed')}
      onConfirm={remove}
      onKeep={() => setAsking(false)}
    />
  )
}
