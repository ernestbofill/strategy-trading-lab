import { useEffect } from 'react'
import { ChoiceCard, ChoiceList } from '../../components/ChoiceCard'
import { ModeTag } from '../../components/ModeTag'
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
  const modes: { id: Mode; line: string; facts: [alerts: string, trades: string, goodFor: string] }[] = [
    {
      id: 'simulation',
      line: t('newStrategy.mode.sim.line'),
      facts: [t('newStrategy.mode.sim.alerts'), t('newStrategy.mode.sim.trades'), t('newStrategy.mode.sim.goodFor')],
    },
    {
      id: 'live',
      line: t('newStrategy.mode.manual.line'),
      facts: [t(alert), t('newStrategy.mode.manual.trades'), t('newStrategy.mode.manual.goodFor')],
    },
  ]
  const labels = [t('newStrategy.mode.alerts'), t('newStrategy.mode.trades'), t('newStrategy.mode.goodFor')]

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
            badge={<ModeTag live={m.id === 'live'} />}
            title={<span className={styles.line}>{m.line}</span>}
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
            <span className={styles.facts}>
              {m.facts.map((text, i) => (
                <span key={labels[i]} className={styles.fact}>
                  <span className={styles.label}>{labels[i]}</span>
                  <span>{text}</span>
                </span>
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
