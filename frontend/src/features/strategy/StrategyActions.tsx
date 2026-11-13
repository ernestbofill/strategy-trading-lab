import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, InvalidFieldsError, InvalidValueError, NameTakenError, UnauthorizedError, type StrategyDetail } from '../../api'
import { TextField } from '../../components/Field'
import { useT } from '../../i18n/I18n'
import { invalidateAll } from '../../lib/useLoadable'
import { useDashboard } from '../dashboard/useDashboard'
import { draftFromStrategy } from '../new-strategy/goLive'
import { defaultName, NAME_MAX_LENGTH } from '../new-strategy/newStrategy'
import { MAX_RUNNING_STRATEGIES } from '../new-strategy/draftState'
import { useNewStrategyDraft } from '../new-strategy/NewStrategyDraft'
import { modePath } from '../new-strategy/paths'
import { useTemplate } from '../new-strategy/useTemplate'
import styles from './StrategyActions.module.css'

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

/** "Archive the strategy", after a confirm step: the daily check stops, waiting signals close as missed, the history stays. */
export function ArchiveStrategy({ s, className, titleClassName }: {
  s: StrategyDetail
  className: string
  titleClassName: string
}) {
  const t = useT()
  const [asking, setAsking] = useState(false)
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  if (!asking)
    return (
      <button type="button" className={className} onClick={() => setAsking(true)}>
        <span className={titleClassName}>{t('strategy.archive')}</span>
      </button>
    )

  const archive = async () => {
    setBusy(true)
    setFailed(false)
    try {
      await api.archiveStrategy(s.id)
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      // Already archived (another tab, another device): show it as it is.
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
      <p>{t('strategy.archiveAsk', { name: s.name })}</p>
      {failed && <p role="alert">{t('strategy.archiveFailed')}</p>}
      <div className={styles.confirmActions}>
        <button type="button" className={styles.btnDanger} disabled={busy} onClick={archive}>
          {busy ? t('strategy.archiving') : t('strategy.archiveConfirm')}
        </button>
        {/* Focus on the safe choice, so the keyboard lands in the confirm step. */}
        <button type="button" className={styles.btnSecondary} disabled={busy} onClick={() => setAsking(false)} autoFocus>
          {t('strategy.archiveKeep')}
        </button>
      </div>
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

  const running = dashboard.status === 'ready' ? dashboard.data.strategies.filter((x) => x.status === 'running') : []
  const atLimit = running.length >= MAX_RUNNING_STRATEGIES

  const goLive = () => {
    if (template.status !== 'ready' || atLimit) return
    const taken = running.map((x) => x.name)
    // Cut before numbering, so a long name keeps its " 2".
    const name = defaultName(t('strategy.goLive.name', { name: s.name }).slice(0, NAME_MAX_LENGTH - 3), taken)
    load(draftFromStrategy(s, template.data, name))
    navigate(modePath)
  }

  // Shown once the template is in: a tile that can't work isn't shown.
  if (template.status !== 'ready') return null

  return (
    <button type="button" className={className} onClick={goLive} disabled={atLimit}>
      <span className={titleClassName}>{t('strategy.tile.goLiveTitle')}</span>
      <span className={detailClassName}>{atLimit ? t('dashboard.strategiesFull', { max: MAX_RUNNING_STRATEGIES }) : t('strategy.tile.goLiveSub')}</span>
    </button>
  )
}
