import { useEffect } from 'react'
import { ChoiceCard, ChoiceList } from '../../components/ChoiceCard'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'
import { useOptionalAccount } from '../account/AccountContext'
import { ResendLink } from '../account/ResendLink'
import styles from './ChooseMode.module.css'
import { useNewStrategyDraft, type Mode } from './NewStrategyDraft'
import { budgetPath, reviewPath } from './paths'
import { useTemplate } from './useTemplate'

/** 3 · Choose the mode (wireframe `Mode`). */
export function ChooseMode() {
  const t = useT()
  const { mode, templateKey, sourceStrategyId, update } = useNewStrategyDraft()
  const template = useTemplate(templateKey)
  const isLive = mode === 'live'
  // Manual's signals come by email, so going manual needs a confirmed address (the server refuses it too). A guest may
  // still pick it: signing up and confirming the address come before the strategy starts.
  const emailVerified = useOptionalAccount()?.emailVerified ?? true

  // A draft that came here live (e.g. picked before the email changed) falls back to a simulation.
  useEffect(() => {
    if (!emailVerified && isLive) update({ mode: 'simulation' })
  }, [emailVerified, isLive, update])

  // Manual's alert line follows the template: AI rounds, buys only, or buys and sells (also while it loads).
  const shown = template.status === 'ready' ? template.data : null
  const alert = shown?.aiRound
    ? 'newStrategy.mode.manual.alertAi'
    : shown?.signals === 'buysOnly'
      ? 'newStrategy.mode.manual.alertBuy'
      : 'newStrategy.mode.manual.alertBuySell'
  const modes: { id: Mode; name: string; line: string; points: string[] }[] = [
    {
      id: 'simulation',
      name: t('newStrategy.mode.sim.name'),
      line: t('newStrategy.mode.sim.line'),
      points: [t('newStrategy.mode.sim.records'), t('newStrategy.mode.sim.summary'), t('newStrategy.mode.sim.for')],
    },
    {
      id: 'live',
      name: t('newStrategy.mode.manual.name'),
      line: t('newStrategy.mode.manual.line'),
      points: [t(alert), t('newStrategy.mode.manual.records'), t('newStrategy.mode.manual.tracker')],
    },
  ]

  return (
    <Screen
      top={<TopBar up={reviewPath(templateKey)} step="3 / 5" />}
      action={<PrimaryAction to={budgetPath}>{t('newStrategy.mode.continue')}</PrimaryAction>}
      // Not for a manual copy already on its way ("Follow it manually").
      note={sourceStrategyId ? undefined : t('newStrategy.mode.later')}
      noteAbove
    >
      <PageHeader title={t('newStrategy.mode.title')} lead={t('newStrategy.mode.lead')} />

      <ChoiceList label={t('newStrategy.mode.listLabel')}>
        {modes.map((m) => (
          <ChoiceCard
            key={m.id}
            name="mode"
            value={m.id}
            checked={mode === m.id}
            // A "Follow it manually" copy switched to Simulation is a new simulation: no source, and the template's name.
            onSelect={() => update(m.id === 'simulation' && sourceStrategyId ? { mode: m.id, sourceStrategyId: null, name: null } : { mode: m.id })}
            title={m.name}
            disabled={m.id === 'live' && !emailVerified}
            footer={
              m.id === 'live' && !emailVerified && (
                <div className={styles.confirmFirst}>
                  <p>{t('newStrategy.mode.manual.confirmFirst')}</p>
                  <ResendLink />
                </div>
              )
            }
          >
            <span className={styles.line}>{m.line}</span>
            {/* Spans, not a list: the card body sits inside the radio's label, which takes only phrasing content. */}
            <span className={styles.points}>
              {m.points.map((point) => (
                <span key={point}>{point}</span>
              ))}
            </span>
          </ChoiceCard>
        ))}
      </ChoiceList>

      {template.status === 'ready' && !template.data.backtest && (
        <ScreenMessage>
          {t('newStrategy.mode.noBacktest', { name: template.data.name })}
        </ScreenMessage>
      )}
    </Screen>
  )
}
