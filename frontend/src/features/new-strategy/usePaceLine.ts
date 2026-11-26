import { useT } from '../../i18n/I18n'
import { monthsToUse } from '../../lib/budget'

/** "At most €400 a month: the budget lasts 8 months or more.", or null without a limit. */
export function usePaceLine(limit: number | null, budget: number, format: (amount: number) => string): string | null {
  const t = useT()
  return limit === null ? null : t('newStrategy.budget.pace', { limit: format(limit), months: monthsToUse(budget, limit) })
}
