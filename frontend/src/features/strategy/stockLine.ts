import type { Currency, StockRow, StockStatusWords, StrategyMode } from '../../api'
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

const steps = (n: number, max: number | null) => (max === null ? `${n} steps` : `${n} of ${max} steps`)

function detailOf(row: StockRow, words: StockStatusWords | null): string {
  const of = row.maxSteps === null ? '' : ` of ${row.maxSteps}`
  switch (row.status) {
    case 'waitingForYou':
      return `Signal: step ${row.signalStep}${of}`
    case 'signal':
      return `Step ${row.signalStep}${of}, bought at the next open`
    case 'holding':
    case 'building':
      // Live, when the user's steps differ from the rules': both.
      return row.rulesSteps !== null && row.rulesSteps !== row.steps
        ? `Rules: ${row.rulesSteps}${of} · You: ${row.steps}${of}`
        : steps(row.steps, row.maxSteps)
    case 'watching':
      return row.distancePct && words ? `${Math.round(row.distancePct)}% from ${words.zone}` : 'Rules not met today'
    case 'notCandidate':
      return words?.notCandidate ?? 'Not a candidate yet'
    case 'notEnoughData':
      return 'Not enough price history yet'
    case 'notChecked':
      return 'Not checked yet'
  }
}

const PILLS: Record<StockRow['status'], [string, PillTone]> = {
  waitingForYou: ['WAITING FOR YOU', 'waiting'],
  signal: ['SIGNAL', 'signal'],
  holding: ['HOLDING', 'holding'],
  building: ['BUILDING', 'building'],
  watching: ['WATCHING', 'watching'],
  notCandidate: ['WATCHING', 'watching'],
  notEnoughData: ['WATCHING', 'watching'],
  notChecked: ['WATCHING', 'watching'],
}

/** The words of a stock's row on the strategy page (spec: Strategy page, one row per stock). */
export function stockLine(row: StockRow, mode: StrategyMode, currency: Currency, words: StockStatusWords | null): StockLine {
  const [pill, tone] = PILLS[row.status]
  const bought = row.result !== null
  return {
    pill,
    tone,
    detail: detailOf(row, words),
    result: bought ? signedPct(row.result!, 1) : '–',
    note: !bought ? 'No buys yet' : `${mode === 'live' ? 'You ' : ''}${money(row.invested, currency)} in`,
  }
}
