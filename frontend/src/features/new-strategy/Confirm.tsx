import { useId, type ReactNode } from 'react'
import type { TemplateDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { BulletList } from '../../components/Lists'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { TextAction } from '../../components/TextAction'
import { DISCLAIMER } from '../../lib/copy'
import { eur } from '../../lib/format'
import styles from './Confirm.module.css'
import { useNewStrategyDraft } from './NewStrategyDraft'
import { advancedPath, budgetPath, dashboardPath, editFromConfirm, modePath, pickPath, reviewPath } from './paths'
import { useTemplate } from './useTemplate'

const NAME_MAX_LENGTH = 60

interface SummaryRow {
  label: string
  value: string
  /** Where "Edit" goes; without it the link is shown but not usable (screen not built yet). */
  to?: string
}

/** 5 · Name, preview and confirm (wireframe `Confirm`). */
export function Confirm() {
  const { templateKey } = useNewStrategyDraft()
  const template = useTemplate(templateKey)
  const top = <TopBar back={budgetPath} step="5 / 5" />

  return (
    <LoadableScreen state={template} top={top} what="the template">
      {(t) => <ConfirmForm template={t} top={top} />}
    </LoadableScreen>
  )
}

function ConfirmForm({ template, top }: { template: TemplateDetail; top: ReactNode }) {
  const draft = useNewStrategyDraft()
  const { mode, budget, step, maxSteps, unticked, update } = draft
  const isLive = mode === 'live'
  // Branch on what the template does, as the budget screen does.
  const allIn = template.signals === 'buysAndSells'
  const isFund = template.watches.kind === 'fund'
  const modeName = isLive ? 'Live' : 'Simulation'
  const inputId = useId()

  // The user has no strategies yet in the mock, so there's nothing to number against.
  const name = draft.name ?? template.name
  // Don't repeat the template's name when the strategy still carries it.
  const shownName = name.trim() || template.name
  const preview = [shownName === template.name ? null : shownName, `Template ${template.name}`, modeName]
    .filter(Boolean)
    .join(' · ')

  const stocksValue = (() => {
    const { watches } = template
    if (watches.kind === 'fund') return watches.fund.name
    const ticked = watches.starterList.filter((s) => !(unticked[template.key] ?? []).includes(s.symbol))
    const more = ticked.length - 1
    return more > 0 ? `${ticked[0].name} and ${more} more` : (ticked[0]?.name ?? '')
  })()

  const rows: SummaryRow[] = [
    { label: 'Template', value: template.name, to: editFromConfirm(pickPath) },
    {
      label: 'Mode',
      value: isLive
        ? `Live: you trade at your broker and confirm each ${template.aiRound ? 'trade' : 'signal'}`
        : 'Simulation: the app trades on paper',
      to: editFromConfirm(modePath),
    },
    { label: 'Stocks', value: stocksValue, to: editFromConfirm(reviewPath(template.key)) },
    { label: 'Settings', value: 'Tested defaults', to: editFromConfirm(advancedPath(template.key)) },
    {
      label: 'Budget',
      value: allIn
        ? eur(budget)
        : `${eur(budget)} · steps of ${eur(step)} · up to ${maxSteps} ${isFund ? 'for the fund' : 'per stock'}`,
      to: editFromConfirm(budgetPath),
    },
    { label: 'Daily overview', value: 'By email, after the US market close' },
    ...(isLive ? [{ label: 'Alerts', value: 'App and email', to: editFromConfirm(modePath) }] : []),
    ...(template.aiRound ? [{ label: 'Prompt', value: 'Suggested', to: editFromConfirm(reviewPath(template.key)) }] : []),
  ]

  const next = [
    ...(template.aiRound
      ? ['There are no past results for this template.', 'Your first AI round is due now.']
      : ['You see right away how the template did in the past.']),
    ...(isLive
      ? [
          'From today the app watches your stocks and alerts you as soon as your rules are met.',
          'You confirm each signal, and the app tracks the results of what you did.',
        ]
      : [
          'From today it runs on real prices and records its own paper trades.',
          'Your first daily overview arrives tomorrow.',
        ]),
  ]

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction to={dashboardPath}>{isLive ? 'Start following it live' : 'Start the simulation'}</PrimaryAction>
      }
      note={DISCLAIMER}
      noteAbove
    >
      <PageHeader title="Ready to start" />

      <div className={styles.name}>
        <label htmlFor={inputId} className={`ui-strong ${styles.label}`}>
          Name your strategy
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
      </div>

      <ul className={styles.summary}>
        {rows.map((r) => (
          <li key={r.label} className={styles.row}>
            <div className={styles.text}>
              <span className={styles.term}>{r.label}</span>
              <p className={styles.value}>{r.value}</p>
            </div>
            <TextAction to={r.to}>
              Edit<span className="visually-hidden"> {r.label.toLowerCase()}</span>
            </TextAction>
          </li>
        ))}
      </ul>

      <Section title="What happens next">
        <BulletList items={next} />
      </Section>
    </Screen>
  )
}
