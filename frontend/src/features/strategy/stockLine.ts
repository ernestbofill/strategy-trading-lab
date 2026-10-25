import type { Currency, StockRow, StockStatusWords, StrategyMode } from '../../api'
import { tNow, type Translate } from '../../i18n/I18n'
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

const steps = (n: number, max: number | null, t: Translate) =>
  max === null ? t('strategy.stock.steps', { n }) : t('strategy.stock.stepsOf', { n, max })
const pct = (d: number) => (d === 0 ? '0' : d < 1 ? '<1' : String(Math.round(d)))
const fillN = (text: string, d: number | null) =>
  d !== null && text.includes('{n}') ? text.replace('{n}', pct(d)) : text.replace('{n}% ', '')

function detailOf(row: StockRow, words: StockStatusWords | null, t: Translate): string {
  const stepOf = (n: number) => t('signal.stepOf', { n, max: row.maxSteps ?? '' })
  const fromZone = (d: number, zone: string) => t('strategy.stock.fromZone', { dist: pct(d), zone })
  switch (row.status) {
    case 'waitingForYou':
      if (row.signalSide === 'sell') return t('strategy.stock.sellWaiting')
      return row.signalStep === null
        ? t('strategy.stock.signalWaiting')
        : t('strategy.stock.signalStep', { step: stepOf(row.signalStep) })
    case 'signal':
      if (row.signalSide === 'sell') return t('strategy.stock.soldNextOpen')
      return row.signalStep === null
        ? t('strategy.stock.boughtNextOpen')
        : t('strategy.stock.stepBoughtNextOpen', { step: stepOf(row.signalStep) })
    case 'done':
      return words?.holding ?? t('strategy.stock.dropBought')
    case 'holding':
    case 'building': {
      const mismatch = row.rulesSteps !== null && row.rulesSteps !== row.steps
      if (row.maxSteps === null) {
        const place = (n: number) => t(n > 0 ? 'strategy.stock.inMarket' : 'strategy.stock.outMarket')
        return mismatch
          ? t('strategy.stock.rulesAndYou', { rules: place(row.rulesSteps ?? 0), you: place(row.steps) })
          : (words?.holding ?? t('strategy.stock.inMarket'))
      }
      // Live, when the user's steps differ from the rules': both.
      return mismatch
        ? t('strategy.stock.rulesAndYou', { rules: stepOf(row.rulesSteps ?? 0), you: stepOf(row.steps) })
        : steps(row.steps, row.maxSteps, t)
    }
    case 'noBudget':
      return t(row.maxSteps === null ? 'strategy.stock.noBudgetShort' : 'strategy.stock.noBudget')
    case 'outOfTheMarket':
      return row.distancePct !== null && row.distancePct > 0 && words
        ? fromZone(row.distancePct, words.zone)
        : (words?.notCandidate ?? t('strategy.stock.rulesNotMet'))
    case 'watching':
      if (words?.watching) {
        return row.distancePct !== null ? fillN(words.watching, row.distancePct) : t('strategy.stock.rulesNotMet')
      }
      // 0 means it is there or past it: the rules aren't met for another reason.
      return row.distancePct !== null && row.distancePct > 0 && words
        ? fromZone(row.distancePct, words.zone)
        : t('strategy.stock.rulesNotMet')
    case 'notCandidate':
      return words?.notCandidate ? fillN(words.notCandidate, row.distancePct) : t('strategy.stock.notCandidate')
    case 'notEnoughData':
      return t('strategy.stock.notEnoughData')
    case 'notChecked':
      return t('strategy.stock.notChecked')
  }
}

const PILL_KEYS: Record<StockRow['status'], [MessageKey, PillTone]> = {
  waitingForYou: ['strategy.pill.waitingForYou', 'waiting'],
  signal: ['strategy.pill.signal', 'signal'],
  holding: ['strategy.pill.holding', 'holding'],
  done: ['strategy.pill.done', 'holding'],
  building: ['strategy.pill.building', 'building'],
  outOfTheMarket: ['strategy.pill.outOfMarket', 'watching'],
  noBudget: ['strategy.pill.watching', 'watching'],
  watching: ['strategy.pill.watching', 'watching'],
  notCandidate: ['strategy.pill.watching', 'watching'],
  notEnoughData: ['strategy.pill.watching', 'watching'],
  notChecked: ['strategy.pill.watching', 'watching'],
}

/** The words of a stock's row on the strategy page (spec: Strategy page, one row per stock). */
export function stockLine(
  row: StockRow,
  mode: StrategyMode,
  currency: Currency,
  words: StockStatusWords | null,
  t: Translate = tNow,
): StockLine {
  const [key, tone] = PILL_KEYS[row.status]
  const amount = money(row.invested, currency)
  return {
    pill: t(key),
    tone,
    detail: detailOf(row, words, t),
    result: row.result === null ? '–' : signedPct(row.result, 1),
    note:
      row.result === null
        ? t('strategy.stock.noBuys')
        : t(mode === 'live' ? 'strategy.stock.investedLive' : 'strategy.stock.investedSim', { amount }),
  }
}
