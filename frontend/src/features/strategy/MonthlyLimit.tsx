import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, type StrategyDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { StepperCard } from '../../components/StepperCard'
import { useLeave } from '../../components/useLeave'
import { useSubmit } from '../../components/useSubmit'
import { useT } from '../../i18n/I18n'
import { monthlyLimitOf, monthsToUse } from '../../lib/budget'
import { money } from '../../lib/format'
import { invalidateAll } from '../../lib/useLoadable'
import { strategyPath } from '../dashboard/paths'
import { useStrategy } from './useStrategy'

/**
 * A running strategy's monthly limit (ROADMAP item 57): at most this much new money a month, unspent room carried over from the
 * strategy's start; at the budget, no limit. Opened from the strategy page's tile.
 */
export function MonthlyLimit() {
  const t = useT()
  const { id = '' } = useParams()
  const top = <TopBar up={strategyPath(id)} upLabel="common.up.strategy" />
  return (
    <LoadableScreen state={useStrategy(id)} top={top} what={t('strategy.what')}>
      {(s) => <MonthlyLimitForm s={s} />}
    </LoadableScreen>
  )
}

function MonthlyLimitForm({ s }: { s: StrategyDetail }) {
  const t = useT()
  const leave = useLeave(strategyPath(s.id))
  const { budget } = s
  const step = budget.step ?? budget.amount
  const [limit, setLimit] = useState<number | null>(budget.monthlyLimit ?? null)
  const { busy, line, run } = useSubmit(t('strategy.limit.failed'))
  const top = <TopBar up={strategyPath(s.id)} upLabel="common.up.strategy" step={s.name} />
  const shown = limit ?? budget.amount
  const set = (value: number) => setLimit(monthlyLimitOf(value, budget.amount, step))

  const save = async () => {
    if (!(await run(() => api.setMonthlyLimit(s.id, limit)))) return
    invalidateAll()
    leave()
  }

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={() => void save()} disabled={busy}>
          {busy ? t('strategy.limit.saving') : t('strategy.limit.save')}
        </PrimaryAction>
      }
    >
      <PageHeader title={t('strategy.limit.title')} lead={t('strategy.limit.lead')} />
      {line && (
        <ScreenMessage>
          <span role="alert">{line.text}</span>
        </ScreenMessage>
      )}
      <StepperCard
        label={t('newStrategy.budget.monthlyLimit')}
        value={shown}
        display={limit === null ? t('newStrategy.budget.noLimit') : money(limit, s.currency)}
        onChange={set}
        help={t('newStrategy.budget.monthlyLimitHelp')}
        onDecrease={() => set(shown - step)}
        onIncrease={() => set(shown + step)}
        canDecrease={shown > step}
        canIncrease={limit !== null}
      />
      {limit !== null && (
        <p>{t('newStrategy.budget.pace', { limit: money(limit, s.currency), months: monthsToUse(budget.amount, limit) })}</p>
      )}
    </Screen>
  )
}
