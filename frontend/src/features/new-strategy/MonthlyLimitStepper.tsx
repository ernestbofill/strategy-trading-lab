import { StepperCard } from '../../components/StepperCard'
import { useT } from '../../i18n/I18n'
import { monthlyLimitOf } from '../../lib/budget'

interface MonthlyLimitStepperProps {
  /** Null: no limit. */
  limit: number | null
  budget: number
  /** One trade's amount: the limit moves by it and never goes under it. */
  step: number
  format: (amount: number) => string
  onChange: (limit: number | null) => void
}

/**
 * "Each month at most" (ROADMAP item 57), on Set your budget and a running strategy's Monthly limit screen: one trade's amount at a
 * time, "No limit" at the budget.
 */
export function MonthlyLimitStepper({ limit, budget, step, format, onChange }: MonthlyLimitStepperProps) {
  const t = useT()
  const shown = limit ?? budget
  const set = (value: number) => onChange(monthlyLimitOf(value, budget, step))
  return (
    <StepperCard
      label={t('newStrategy.budget.monthlyLimit')}
      value={shown}
      display={limit === null ? t('newStrategy.budget.noLimit') : format(limit)}
      onChange={set}
      help={t('newStrategy.budget.monthlyLimitHelp')}
      onDecrease={() => set(shown - step)}
      onIncrease={() => set(shown + step)}
      canDecrease={shown > step}
      canIncrease={limit !== null}
    />
  )
}
