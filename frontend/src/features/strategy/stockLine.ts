import type { Currency, StockRow, StockStatusWords, StrategyMode } from '../../api'
import type { Translate } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { money, signedPct } from '../../lib/format'

export type PillTone = 'waiting' | 'signal' | 'holding' | 'building' | 'watching'

/** What a stock's row says: its pill, the line under it, and its result on the right. */
export interface StockLine {
  pill: string
  tone: PillTone
  detail: string
  result: string
  note: string
}

const steps = (n: number, max: number | null, t?: Translate) =>
  max === null
    ? t ? t('strategy.stock.steps', { n }) : `${n} steps`
    : t ? t('strategy.stock.stepsOf', { n, max }) : `${n} of ${max} steps`

function detailOf(row: StockRow, words: StockStatusWords | null, t?: Translate): string {
  const of = row.maxSteps === null ? '' : ` of ${row.maxSteps}`
  switch (row.status) {
    case 'waitingForYou':
      return row.signalStep === null
        ? t ? t('strategy.stock.signalWaiting') : 'Signal waiting'
        : t ? t('strategy.stock.signalStep', { step: t('signal.stepOf', { n: row.signalStep, max: row.maxSteps ?? '' }) }) : `Signal: step ${row.signalStep}${of}`
    case 'signal':
      return row.signalStep === null
        ? t ? t('strategy.stock.boughtNextOpen') : 'Bought at the next open'
        : t ? t('strategy.stock.stepBoughtNextOpen', { step: t('signal.stepOf', { n: row.signalStep, max: row.maxSteps ?? '' }) }) : `Step ${row.signalStep}${of}, bought at the next open`
    case 'holding':
    case 'building':
      // Live, when the user's steps differ from the rules': both.
      return row.rulesSteps !== null && row.rulesSteps !== row.steps
        ? t
          ? t('strategy.stock.rulesAndYou', {
              rules: t('signal.stepOf', { n: row.rulesSteps, max: row.maxSteps ?? '' }),
              you: t('signal.stepOf', { n: row.steps, max: row.maxSteps ?? '' }),
            })
          : `Rules: ${row.rulesSteps}${of} · You: ${row.steps}${of}`
        : steps(row.steps, row.maxSteps, t)
    case 'noBudget':
      return t ? t('strategy.stock.noBudget') : 'No budget left for its steps'
    case 'watching':
      // 0 means it is there or past it: the rules aren't met for another reason.
      return row.distancePct !== null && row.distancePct > 0 && words
        ? t
          ? t('strategy.stock.fromZone', { dist: row.distancePct < 1 ? '<1' : Math.round(row.distancePct), zone: words.zone })
          : `${row.distancePct < 1 ? '<1' : Math.round(row.distancePct)}% from ${words.zone}`
        : t ? t('strategy.stock.rulesNotMet') : 'Rules not met today'
    case 'notCandidate':
      return words?.notCandidate ?? (t ? t('strategy.stock.notCandidate') : 'Not a candidate yet')
    case 'notEnoughData':
      return t ? t('strategy.stock.notEnoughData') : 'Not enough price history yet'
    case 'notChecked':
      return t ? t('strategy.stock.notChecked') : 'Not checked yet'
  }
}

const PILL_KEYS: Record<StockRow['status'], [MessageKey, PillTone, string]> = {
  waitingForYou: ['strategy.pill.waitingForYou', 'waiting', 'WAITING FOR YOU'],
  signal: ['strategy.pill.signal', 'signal', 'SIGNAL'],
  holding: ['strategy.pill.holding', 'holding', 'HOLDING'],
  building: ['strategy.pill.building', 'building', 'BUILDING'],
  noBudget: ['strategy.pill.watching', 'watching', 'WATCHING'],
  watching: ['strategy.pill.watching', 'watching', 'WATCHING'],
  notCandidate: ['strategy.pill.watching', 'watching', 'WATCHING'],
  notEnoughData: ['strategy.pill.watching', 'watching', 'WATCHING'],
  notChecked: ['strategy.pill.watching', 'watching', 'WATCHING'],
}

/** The words of a stock's row on the strategy page (spec: Strategy page, one row per stock). */
export function stockLine(row: StockRow, mode: StrategyMode, currency: Currency, words: StockStatusWords | null, t?: Translate): StockLine {
  const [key, tone, fallback] = PILL_KEYS[row.status]
  const pill = t ? t(key) : fallback
  return {
    pill,
    tone,
    detail: detailOf(row, words, t),
    result: row.result === null ? '–' : signedPct(row.result, 1),
    note:
      row.result === null
        ? t ? t('strategy.stock.noBuys') : 'No buys yet'
        : t
        ? mode === 'live'
          ? t('strategy.stock.investedLive', { amount: money(row.invested, currency) })
          : t('strategy.stock.investedSim', { amount: money(row.invested, currency) })
        : `${mode === 'live' ? 'You ' : ''}${money(row.invested, currency)} in`,
  }
}
