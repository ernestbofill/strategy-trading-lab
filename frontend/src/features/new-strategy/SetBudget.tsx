import type { ReactNode } from 'react'
import type { TemplateDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { StepperCard } from '../../components/StepperCard'
import { Tag } from '../../components/Tag'
import { useT } from '../../i18n/I18n'
import {
  BUDGET_LIMITS,
  MAX_STEPS_LIMITS,
  STEP_LIMITS,
  clamp,
  resolveBudget,
  summarize,
  daysToUse,
} from '../../lib/budget'
import { currencyName, money } from '../../lib/format'
import { useOptionalAccount } from '../account/AccountContext'
import { useNewStrategyDraft } from './NewStrategyDraft'
import { confirmPath, modePath } from './paths'
import styles from './SetBudget.module.css'
import { useTemplate } from './useTemplate'

/** 4 · Set your budget (wireframe `Budget`). */
export function SetBudget() {
  const t = useT()
  const { templateKey } = useNewStrategyDraft()
  const template = useTemplate(templateKey)
  const top = <TopBar back={modePath} step="4 / 5" />

  return (
    <LoadableScreen state={template} top={top} what={t('newStrategy.what.template')}>
      {(tDetail) => <BudgetForm template={tDetail} top={top} />}
    </LoadableScreen>
  )
}

function BudgetForm({ template, top }: { template: TemplateDetail; top: ReactNode }) {
  const t = useT()
  const draft = useNewStrategyDraft()
  const { mode, update } = draft
  const account = useOptionalAccount()
  // A live copy keeps its simulation's currency; guests default to EUR.
  const currency = draft.currency ?? account?.currency ?? 'EUR'
  const amount = (n: number) => money(n, currency)
  const { budget, step, maxSteps } = resolveBudget(draft, template.defaults.budget)
  const isLive = mode === 'live'
  // Branch on what the template does, not on its key.
  const allIn = template.signals === 'buysAndSells'
  const isFund = template.watches.kind === 'fund'
  const { perStock, totalSteps, fullStocks, shown } = summarize(budget, step, maxSteps)

  const setBudget = (value: number) => {
    const next = clamp(value, BUDGET_LIMITS.min, BUDGET_LIMITS.max)
    // A step can't be more than the budget; otherwise it keeps following the tested default.
    update(step > next ? { budget: next, step: next } : { budget: next })
  }

  const steps = t('newStrategy.budget.steps', { n: totalSteps })
  const upTo = t('newStrategy.budget.steps', { n: maxSteps })
  const sentence = allIn
    ? t('newStrategy.budget.sentenceAllIn', { budget: amount(budget) })
    : isFund
      ? t('newStrategy.budget.sentenceFund', { step: amount(step), upTo, perStock: amount(perStock), steps })
      : t('newStrategy.budget.sentenceStock', {
          step: amount(step),
          upTo,
          perStock: amount(perStock),
          steps,
          stocks: t('newStrategy.budget.stocks', { n: fullStocks }),
        })

  return (
    <Screen
      top={top}
      action={<PrimaryAction to={confirmPath}>{t('newStrategy.budget.continue')}</PrimaryAction>}
      note={
        currency === 'USD'
          ? t('newStrategy.budget.noteUsd')
          : t('newStrategy.budget.noteOther', { currency: currencyName(currency) })
      }
    >
      <PageHeader
        eyebrow={
          <>
            <Tag tone={isLive ? 'live' : 'sim'}>{isLive ? 'LIVE' : 'SIM'}</Tag> {template.name}
          </>
        }
        title={t('newStrategy.budget.title')}
        lead={isLive ? t('newStrategy.budget.leadLive') : t('newStrategy.budget.leadSim')}
      />

      <div className={styles.settings}>
        <StepperCard
          label={t('newStrategy.budget.budget')}
          value={budget}
          display={amount(budget)}
          onChange={setBudget}
          help={t('newStrategy.budget.budgetHelp')}
          onDecrease={() => setBudget(budget - BUDGET_LIMITS.stepBy)}
          onIncrease={() => setBudget(budget + BUDGET_LIMITS.stepBy)}
          canDecrease={budget > BUDGET_LIMITS.min}
          canIncrease={budget < BUDGET_LIMITS.max}
        />
        {!allIn && (
          <>
            <StepperCard
              label={t('newStrategy.budget.stepSize')}
              value={step}
              display={amount(step)}
              onChange={(v) => update({ step: clamp(v, STEP_LIMITS.min, budget) })}
              help={t('newStrategy.budget.stepSizeHelp')}
              onDecrease={() => update({ step: clamp(step - STEP_LIMITS.stepBy, STEP_LIMITS.min, budget) })}
              onIncrease={() => update({ step: clamp(step + STEP_LIMITS.stepBy, STEP_LIMITS.min, budget) })}
              canDecrease={step > STEP_LIMITS.min}
              canIncrease={step < budget}
            />
            <StepperCard
              label={isFund ? t('newStrategy.budget.maxStepsFund') : t('newStrategy.budget.maxStepsStock')}
              value={maxSteps}
              display={String(maxSteps)}
              onChange={(v) => update({ maxSteps: clamp(v, MAX_STEPS_LIMITS.min, MAX_STEPS_LIMITS.max) })}
              help={isFund ? t('newStrategy.budget.maxStepsHelpFund') : t('newStrategy.budget.maxStepsHelpStock')}
              onDecrease={() => update({ maxSteps: clamp(maxSteps - 1, MAX_STEPS_LIMITS.min, MAX_STEPS_LIMITS.max) })}
              onIncrease={() => update({ maxSteps: clamp(maxSteps + 1, MAX_STEPS_LIMITS.min, MAX_STEPS_LIMITS.max) })}
              canDecrease={maxSteps > MAX_STEPS_LIMITS.min}
              canIncrease={maxSteps < MAX_STEPS_LIMITS.max}
            />
          </>
        )}
      </div>

      <div className={styles.summary}>
        <p className={styles.sentence}>{sentence}</p>
        {!allIn && (
          <div>
            <ul className={styles.squares} aria-hidden="true">
              {Array.from({ length: shown }, (_, i) => (
                <li key={i} className={styles.square} data-alt={Math.floor(i / maxSteps) % 2 === 1 || undefined} />
              ))}
            </ul>
            <div className={styles.caption}>
              {totalSteps > shown
                ? t('newStrategy.budget.firstSteps', { shown, total: totalSteps })
                : t('newStrategy.budget.stepsInBudget', { steps })}
            </div>
          </div>
        )}
        {template.aiRound && !allIn && (
          <p className={styles.last}>
            {t('newStrategy.budget.aiPace', {
              every: template.aiRound.everyDays,
              total: daysToUse(totalSteps, template.aiRound.everyDays),
            })}
          </p>
        )}
        {!template.aiRound && (
          <p className={styles.last}>
            {isLive ? t('newStrategy.budget.lastLive') : t('newStrategy.budget.lastSim')}
          </p>
        )}
      </div>
    </Screen>
  )
}
