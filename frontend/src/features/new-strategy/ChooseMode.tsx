import { ChoiceCard, ChoiceList } from '../../components/ChoiceCard'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Tag, type TagTone } from '../../components/Tag'
import { useT } from '../../i18n/I18n'
import styles from './ChooseMode.module.css'
import { useNewStrategyDraft, type Mode } from './NewStrategyDraft'
import { budgetPath, reviewPath } from './paths'
import { useTemplate } from './useTemplate'

/** 3 · Choose the mode (wireframe `Mode`). */
export function ChooseMode() {
  const t = useT()
  const { mode, templateKey, update } = useNewStrategyDraft()
  const template = useTemplate(templateKey)
  const isLive = mode === 'live'

  const modes: { id: Mode; tag: TagTone; badge: string; name: string; line: string; facts: [label: string, text: string][] }[] = [
    {
      id: 'simulation',
      tag: 'sim',
      badge: t('newStrategy.mode.tagSim'),
      name: t('newStrategy.mode.sim.name'),
      line: t('newStrategy.mode.sim.line'),
      facts: [
        [t('newStrategy.mode.who'), t('newStrategy.mode.sim.who')],
        [t('newStrategy.mode.app'), t('newStrategy.mode.sim.app')],
        [t('newStrategy.mode.you'), t('newStrategy.mode.sim.you')],
      ],
    },
    {
      id: 'live',
      tag: 'live',
      badge: t('newStrategy.mode.tagLive'),
      name: t('newStrategy.mode.live.name'),
      line: t('newStrategy.mode.live.line'),
      facts: [
        [t('newStrategy.mode.who'), t('newStrategy.mode.live.who')],
        [t('newStrategy.mode.app'), t('newStrategy.mode.live.app')],
        [t('newStrategy.mode.you'), t('newStrategy.mode.live.you')],
      ],
    },
  ]

  return (
    <Screen
      top={<TopBar back={reviewPath(templateKey)} step="3 / 5" />}
      action={<PrimaryAction to={budgetPath}>{t('newStrategy.mode.continue')}</PrimaryAction>}
      note={
        isLive && (
          <span className={styles.inkNote}>
            {t('newStrategy.mode.liveNote')}
          </span>
        )
      }
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
            onSelect={() => update({ mode: m.id })}
            badge={<Tag tone={m.tag}>{m.badge}</Tag>}
            title={m.name}
          >
            <span className={styles.line}>{m.line}</span>
            <span className={styles.facts}>
              {m.facts.map(([label, text]) => (
                <span key={label}>
                  <span className={styles.label}>{label}</span> {text}
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
