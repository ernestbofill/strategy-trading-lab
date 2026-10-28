import { useId, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, NameTakenError, type TemplateDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { BulletList } from '../../components/Lists'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { resolveBudget } from '../../lib/budget'
import { money } from '../../lib/format'
import { useCurrentAccount } from '../account/AccountContext'
import { accountEmailsPath } from '../account/paths'
import { invalidate } from '../../lib/useLoadable'
import { dashboardCacheKey, useDashboard } from '../dashboard/useDashboard'
import styles from './Confirm.module.css'
import { fundOf, tickedStocks } from './draftState'
import { useNewStrategyDraft } from './NewStrategyDraft'
import { settingsSummary } from './settings'
import { defaultName, NAME_MAX_LENGTH, newStrategyFrom } from './newStrategy'
import { advancedPath, budgetPath, dashboardPath, modePath, pickPath, promptPath, reviewPath } from './paths'
import { useTemplate } from './useTemplate'

interface SummaryRow {
  label: string
  value: string
  /** Where "Edit" goes; without it there is no "Edit" (its screen isn't built yet). */
  to?: string
}

/** 5 · Name, preview and confirm (wireframe `Confirm`). */
export function Confirm() {
  const t = useT()
  const { templateKey } = useNewStrategyDraft()
  const template = useTemplate(templateKey)
  const top = <TopBar back={budgetPath} step="5 / 5" />

  return (
    <LoadableScreen state={template} top={top} what={t('newStrategy.what.template')}>
      {(tDetail) => <ConfirmForm template={tDetail} top={top} />}
    </LoadableScreen>
  )
}

function ConfirmForm({ template, top }: { template: TemplateDetail; top: ReactNode }) {
  const t = useT()
  const draft = useNewStrategyDraft()
  const { mode, update, reset } = draft
  const account = useCurrentAccount()
  const { emailVerified } = account
  // A live copy keeps its simulation's currency.
  const currency = draft.currency ?? account.currency
  const amount = (n: number) => money(n, currency)
  const { budget, step, maxSteps } = resolveBudget(draft, template.defaults.budget)
  const isLive = mode === 'live'
  // Branch on what the template does, as the budget screen does.
  const allIn = template.signals === 'buysAndSells'
  const isFund = template.watches.kind === 'fund'
  const modeName = isLive ? t('newStrategy.mode.live.name') : t('newStrategy.mode.sim.name')
  const inputId = useId()
  const navigate = useNavigate()
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Numbered against the user's strategies once they are loaded; the server still refuses a name that is taken.
  const dashboard = useDashboard()
  const taken = dashboard.status === 'ready' ? dashboard.data.strategies.map((s) => s.name) : []
  const fallback = defaultName(template.name, taken)
  const name = draft.name ?? fallback
  // Don't repeat the template's name when the strategy still carries it.
  const shownName = name.trim() || fallback
  const preview = [shownName === template.name ? null : shownName, t('newStrategy.confirm.preview', { name: template.name }), modeName]
    .filter(Boolean)
    .join(' · ')

  const start = async () => {
    setStarting(true)
    setError(null)
    try {
      await api.startStrategy(newStrategyFrom(draft, template, shownName, currency))
    } catch (e) {
      setError(
        e instanceof NameTakenError
          ? t('newStrategy.confirm.nameTaken')
          : isLive && !emailVerified
            ? t('newStrategy.mode.live.confirmFirst')
            : t('newStrategy.confirm.startFailed'),
      )
      setStarting(false)
      return
    }
    // The dashboard loaded before; make it ask again so it lists the new strategy.
    invalidate(dashboardCacheKey)
    // Replace this screen in the history: going back must not reopen Confirm with the reset draft, which would
    // start a second strategy from the default template. An AI strategy's first round waits on the dashboard.
    navigate(dashboardPath, { replace: true })
    reset()
  }

  const stocksValue = (() => {
    const { watches } = template
    if (watches.kind === 'fund') return fundOf(draft, template)!.name
    const ticked = tickedStocks(draft, template)
    const more = ticked.length - 1
    return more > 0
      ? t('newStrategy.confirm.stocksMore', { first: ticked[0].name, n: more })
      : (ticked[0]?.name ?? '')
  })()

  const rows: SummaryRow[] = [
    { label: t('newStrategy.confirm.row.template'), value: template.name, to: pickPath },
    {
      label: t('newStrategy.confirm.row.mode'),
      value: isLive
        ? template.aiRound
          ? t('newStrategy.confirm.modeLiveTrade')
          : t('newStrategy.confirm.modeLiveSignal')
        : t('newStrategy.confirm.modeSim'),
      to: modePath,
    },
    { label: t('newStrategy.confirm.row.stocks'), value: stocksValue, to: reviewPath(template.key) },
    // Only templates with settings to tune list them.
    ...(template.settings.length > 0
      ? [{ label: t('newStrategy.confirm.row.settings'), value: settingsSummary(draft.settings), to: advancedPath(template.key) }]
      : []),
    {
      label: t('newStrategy.confirm.row.budget'),
      value: allIn
        ? amount(budget)
        : isFund
          ? t('newStrategy.confirm.budgetStepsFund', { budget: amount(budget), step: amount(step), max: maxSteps })
          : t('newStrategy.confirm.budgetStepsStock', { budget: amount(budget), step: amount(step), max: maxSteps }),
      to: budgetPath,
    },
    { label: t('newStrategy.confirm.row.overview'), value: t('newStrategy.confirm.overview'), to: accountEmailsPath },
    ...(isLive ? [{ label: t('newStrategy.confirm.row.alerts'), value: t('newStrategy.confirm.alerts'), to: modePath }] : []),
    ...(template.aiRound
      ? [
          {
            label: t('newStrategy.confirm.row.prompt'),
            value: t(draft.aiInstructions === null ? 'newStrategy.confirm.prompt' : 'newStrategy.confirm.promptEdited'),
            to: promptPath(template.key),
          },
        ]
      : []),
  ]

  const next = [
    ...(template.aiRound
      ? [t('newStrategy.confirm.next.noPast'), t('newStrategy.confirm.next.firstAi')]
      : [t('newStrategy.confirm.next.seePast')]),
    ...(isLive
      ? [
          t('newStrategy.confirm.next.liveWatch'),
          t('newStrategy.confirm.next.liveConfirm'),
        ]
      : [
          t('newStrategy.confirm.next.simRuns'),
          t('newStrategy.confirm.next.simFirst'),
        ]),
  ]

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={start} disabled={starting}>
          {starting ? t('newStrategy.confirm.starting') : isLive ? t('newStrategy.confirm.startLive') : t('newStrategy.confirm.startSim')}
        </PrimaryAction>
      }
      note={t('common.disclaimerFull')}
      noteAbove
    >
      <PageHeader title={t('newStrategy.confirm.title')} />

      <div className={styles.name}>
        <label htmlFor={inputId} className={`ui-strong ${styles.label}`}>
          {t('newStrategy.confirm.nameLabel')}
        </label>
        <input
          id={inputId}
          className={styles.input}
          type="text"
          value={name}
          maxLength={NAME_MAX_LENGTH}
          onChange={(e) => update({ name: e.target.value })}
          // An emptied field goes back to the template's name.
          onBlur={() => !name.trim() && update({ name: null })}
        />
        <p className={styles.preview}>{preview}</p>
        {error && (
          <p role="alert" className={styles.error}>
            {error}
          </p>
        )}
      </div>

      <ul className={styles.summary}>
        {rows.map((r) => (
          <li key={r.label} className={styles.row}>
            <div className={styles.text}>
              <span className={styles.term}>{r.label}</span>
              <p className={styles.value}>{r.value}</p>
            </div>
            {r.to && (
              <TextAction to={r.to}>
                {t('newStrategy.confirm.edit')}<span className="visually-hidden"> {r.label.toLowerCase()}</span>
              </TextAction>
            )}
          </li>
        ))}
      </ul>

      <Section title={t('newStrategy.confirm.next')}>
        <BulletList items={next} />
      </Section>
    </Screen>
  )
}
