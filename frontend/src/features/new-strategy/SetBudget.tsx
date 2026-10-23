import type { ReactNode } from 'react'
import type { TemplateDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { StepperCard } from '../../components/StepperCard'
import { Tag } from '../../components/Tag'
import {
  BUDGET_LIMITS,
  MAX_STEPS_LIMITS,
  STEP_LIMITS,
  clamp,
  plural,
  resolveBudget,
  summarize,
  weeksToUse,
} from '../../lib/budget'
import { CURRENCIES, money } from '../../lib/format'
import { useCurrentAccount } from '../account/AccountContext'
import { useNewStrategyDraft } from './NewStrategyDraft'
import { confirmPath, modePath } from './paths'
import styles from './SetBudget.module.css'
import { useTemplate } from './useTemplate'

/** 4 · Set your budget (wireframe `Budget`). */
export function SetBudget() {
  const { templateKey } = useNewStrategyDraft()
  const template = useTemplate(templateKey)
  const top = <TopBar back={modePath} step="4 / 5" />

  return (
    <LoadableScreen state={template} top={top} what="the template">
      {(t) => <BudgetForm template={t} top={top} />}
    </LoadableScreen>
  )
}

function BudgetForm({ template, top }: { template: TemplateDetail; top: ReactNode }) {
  const draft = useNewStrategyDraft()
  const { mode, update } = draft
  const { currency } = useCurrentAccount()
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

  const steps = plural(totalSteps, 'step', 'steps')
  const upTo = plural(maxSteps, 'step', 'steps')
  const sentence = allIn
    ? `The whole budget of ${amount(budget)} goes in when the rules say so, and comes out when they say so.`
    : isFund
      ? `Each buy is ${amount(step)}. The fund gets at most ${upTo} (${amount(perStock)}). The budget covers ${steps}.`
      : `Each buy is ${amount(step)}. A stock gets at most ${upTo} (${amount(perStock)}). The budget covers ${steps}, enough for ${plural(fullStocks, 'stock', 'stocks')} bought in full.`

  return (
    <Screen
      top={top}
      action={<PrimaryAction to={confirmPath}>Continue</PrimaryAction>}
      note={`Amounts in ${CURRENCIES.find((c) => c.code === currency)?.name.toLowerCase()}. Dollar prices convert at the day's rate.`}
    >
      <PageHeader
        eyebrow={
          <>
            <Tag tone={isLive ? 'live' : 'sim'}>{isLive ? 'LIVE' : 'SIM'}</Tag> {template.name}
          </>
        }
        title="Set your budget"
        lead={
          isLive
            ? "The amount you plan to put into this strategy at your broker. The app doesn't move or hold any money."
            : 'A what-if amount. No real money moves.'
        }
      />

      <div className={styles.settings}>
        <StepperCard
          label="Budget"
          value={budget}
          display={amount(budget)}
          onChange={setBudget}
          help="The most this strategy can invest in total."
          onDecrease={() => setBudget(budget - BUDGET_LIMITS.stepBy)}
          onIncrease={() => setBudget(budget + BUDGET_LIMITS.stepBy)}
          canDecrease={budget > BUDGET_LIMITS.min}
          canIncrease={budget < BUDGET_LIMITS.max}
        />
        {!allIn && (
          <>
            <StepperCard
              label="Step size"
              value={step}
              display={amount(step)}
              onChange={(v) => update({ step: clamp(v, STEP_LIMITS.min, budget) })}
              help="How much each buy is."
              onDecrease={() => update({ step: clamp(step - STEP_LIMITS.stepBy, STEP_LIMITS.min, budget) })}
              onIncrease={() => update({ step: clamp(step + STEP_LIMITS.stepBy, STEP_LIMITS.min, budget) })}
              canDecrease={step > STEP_LIMITS.min}
              canIncrease={step < budget}
            />
            <StepperCard
              label={isFund ? 'Steps for the fund' : 'Max steps per stock'}
              value={maxSteps}
              display={String(maxSteps)}
              onChange={(v) => update({ maxSteps: clamp(v, MAX_STEPS_LIMITS.min, MAX_STEPS_LIMITS.max) })}
              help={`Buying ${isFund ? 'the fund' : 'a stock'} in steps limits the damage of a bad first buy.`}
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
              {totalSteps > shown ? `First ${shown} of ${totalSteps} steps` : `${steps} in the budget`}
            </div>
          </div>
        )}
        {template.aiRound && !allIn && (
          <p className={styles.last}>
            One step every {plural(template.aiRound.everyWeeks, 'week', 'weeks').replace(/^1 /, '')} uses the budget in
            about {weeksToUse(totalSteps, template.aiRound.everyWeeks)} weeks.
          </p>
        )}
        {!template.aiRound && (
          <p className={styles.last}>
            {isLive
              ? "When the budget is used up, the strategy stops signalling buys. Only trades you confirm use the budget; skipped signals don't."
              : 'When the budget is used up, the strategy stops buying.'}
          </p>
        )}
      </div>
    </Screen>
  )
}
