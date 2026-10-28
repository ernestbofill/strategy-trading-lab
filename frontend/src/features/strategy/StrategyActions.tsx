import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, InvalidFieldsError, InvalidValueError, NameTakenError, UnauthorizedError, type StrategyDetail } from '../../api'
import { Checkbox, FormMessage, TextField } from '../../components/Field'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { invalidate, invalidateAll } from '../../lib/useLoadable'
import { dashboardCacheKey, useDashboard } from '../dashboard/useDashboard'
import { draftFromStrategy } from '../new-strategy/goLive'
import { defaultName, NAME_MAX_LENGTH } from '../new-strategy/newStrategy'
import { useNewStrategyDraft } from '../new-strategy/NewStrategyDraft'
import { modePath } from '../new-strategy/paths'
import { useTemplate } from '../new-strategy/useTemplate'
import styles from './StrategyActions.module.css'
import { strategyCacheKey } from './useStrategy'

/** The page and the dashboard show the change. */
const reload = (id: string) => {
  invalidate(strategyCacheKey(id))
  invalidate(dashboardCacheKey)
}

/** "RENAME": the heading becomes a field with Save and Cancel. Confirm's rules: 1 to 60 characters, unique among running strategies. */
export function RenameForm({ s, onClose }: { s: StrategyDetail; onClose: () => void }) {
  const t = useT()
  const [name, setName] = useState(s.name)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const save = async (e: FormEvent) => {
    e.preventDefault()
    if (name.trim() === s.name) return onClose()
    setBusy(true)
    setError(null)
    try {
      await api.renameStrategy(s.id, name.trim())
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setError(
        err instanceof NameTakenError
          ? t('strategy.renameTaken')
          : err instanceof InvalidFieldsError
            ? (err.fields.name ?? err.message)
            : t('strategy.renameFailed'),
      )
      setBusy(false)
      return
    }
    // The name shows on the signal and AI round pages too.
    invalidateAll()
    onClose()
  }

  return (
    <form className={styles.renameForm} onSubmit={save}>
      <TextField label={t('strategy.renameLabel')} value={name} onChange={setName} error={error} />
      <div className={styles.confirmActions}>
        <button type="submit" className={styles.btnPrimary} disabled={busy}>
          {t('strategy.renameSave')}
        </button>
        <button type="button" className={styles.btnSecondary} disabled={busy} onClick={onClose}>
          {t('strategy.renameCancel')}
        </button>
      </div>
    </form>
  )
}

/** The alerts switch, for strategies that email: live ones (signals) and AI ones (rounds due). */
export function AlertsSwitch({ s }: { s: StrategyDetail }) {
  const t = useT()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const live = s.mode === 'live'
  const label = live && s.aiRounds ? t('strategy.alerts.both') : live ? t('strategy.alerts.signals') : t('strategy.alerts.rounds')

  const change = async (enabled: boolean) => {
    if (busy) return
    setBusy(true)
    setFailed(false)
    try {
      await api.setStrategyNotifications(s.id, enabled)
      invalidate(strategyCacheKey(s.id))
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setFailed(true)
    }
    setBusy(false)
  }

  return (
    <div className={styles.alerts}>
      <Checkbox checked={s.notifications} onChange={(enabled) => void change(enabled)}>
        {label}
      </Checkbox>
      <p className="caption">{t('strategy.alerts.hint')}</p>
      {failed && <FormMessage tone="error">{t('strategy.alertsFailed')}</FormMessage>}
    </div>
  )
}

/** "End the strategy", after a confirm step: the daily check stops, waiting signals close as missed, the history stays. */
export function EndStrategy({ s }: { s: StrategyDetail }) {
  const t = useT()
  const [asking, setAsking] = useState(false)
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  if (!asking)
    return (
      <div className={styles.start}>
        <TextAction onClick={() => setAsking(true)}>{t('strategy.end')}</TextAction>
      </div>
    )

  const end = async () => {
    setBusy(true)
    setFailed(false)
    try {
      await api.endStrategy(s.id)
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      // Already ended (another tab, another device): show it as it is.
      if (!(err instanceof InvalidValueError)) {
        setFailed(true)
        setBusy(false)
        return
      }
    }
    // Its signals, AI rounds and the dashboard all change.
    invalidateAll()
  }

  return (
    <div className={styles.confirmBox}>
      <p>{t('strategy.endAsk', { name: s.name })}</p>
      {failed && <p role="alert">{t('strategy.endFailed')}</p>}
      <div className={styles.confirmActions}>
        <button type="button" className={styles.btnDanger} disabled={busy} onClick={end}>
          {busy ? t('strategy.ending') : t('strategy.endConfirm')}
        </button>
        {/* Focus on the safe choice, so the keyboard lands in the confirm step. */}
        <button type="button" className={styles.btnSecondary} disabled={busy} onClick={() => setAsking(false)} autoFocus>
          {t('strategy.endKeep')}
        </button>
      </div>
    </div>
  )
}

/** "Archive" (or "Unarchive") an ended strategy: it moves to the dashboard's collapsed Archived section. */
export function ArchiveStrategy({ s }: { s: StrategyDetail }) {
  const t = useT()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  const toggle = async () => {
    setBusy(true)
    setFailed(false)
    try {
      await api.archiveStrategy(s.id, !s.archived)
      reload(s.id)
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setFailed(true)
    }
    setBusy(false)
  }

  return (
    <div className={styles.start}>
      <TextAction onClick={() => void toggle()} disabled={busy}>
        {s.archived ? t('strategy.unarchive') : t('strategy.archive')}
      </TextAction>
      {failed && <p role="alert">{t('strategy.archiveFailed')}</p>}
    </div>
  )
}

/**
 * "Go live with these settings", on a running simulation: the new-strategy draft takes its stocks, settings, budget and
 * currency, named "[name] live" (numbered while a running strategy has that name), and Choose the mode opens with Live picked.
 */
export function GoLive({ s, className, titleClassName, detailClassName }: {
  s: StrategyDetail
  className: string
  titleClassName: string
  detailClassName: string
}) {
  const t = useT()
  const navigate = useNavigate()
  const { load } = useNewStrategyDraft()
  const template = useTemplate(s.templateKey)
  const dashboard = useDashboard()

  const goLive = () => {
    if (template.status !== 'ready') return
    const taken = dashboard.status === 'ready' ? dashboard.data.strategies.filter((x) => x.status === 'running').map((x) => x.name) : []
    const name = defaultName(t('strategy.goLive.name', { name: s.name }), taken).slice(0, NAME_MAX_LENGTH)
    load(draftFromStrategy(s, template.data, name))
    navigate(modePath)
  }

  return (
    <button type="button" className={className} onClick={goLive} disabled={template.status !== 'ready'}>
      <span className={titleClassName}>{t('strategy.tile.goLiveTitle')}</span>
      <span className={detailClassName}>{t('strategy.tile.goLiveSub')}</span>
    </button>
  )
}
