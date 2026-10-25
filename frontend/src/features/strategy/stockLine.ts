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
const pct = (d: number) => (d === 0 ? '0' : d < 1 ? '<1' : String(Math.round(d)))
const fillN = (text: string, d: number | null) =>
  d !== null && text.includes('{n}') ? text.replace('{n}', pct(d)) : text.replace('{n}% ', '')

function detailOf(row: StockRow, words: StockStatusWords | null): string {
  const of = row.maxSteps === null ? '' : ` of ${row.maxSteps}`
  switch (row.status) {
    case 'waitingForYou':
      if (row.signalSide === 'sell') return 'Sell signal waiting'
      return row.signalStep === null ? 'Signal waiting' : `Signal: step ${row.signalStep}${of}`
    case 'signal':
      if (row.signalSide === 'sell') return 'Sold at the next open'
      return row.signalStep === null ? 'Bought at the next open' : `Step ${row.signalStep}${of}, bought at the next open`
    case 'done':
      return words?.holding ?? "This drop's steps are bought"
    case 'holding':
    case 'building':
      if (row.maxSteps === null) {
        return row.rulesSteps !== null && row.rulesSteps !== row.steps
          ? `Rules: ${row.rulesSteps > 0 ? 'in the market' : 'out of the market'} · You: ${row.steps > 0 ? 'in the market' : 'out of the market'}`
          : (words?.holding ?? 'In the market')
      }
      // Live, when the user's steps differ from the rules': both.
      return row.rulesSteps !== null && row.rulesSteps !== row.steps
        ? `Rules: ${row.rulesSteps}${of} · You: ${row.steps}${of}`
        : steps(row.steps, row.maxSteps)
    case 'noBudget':
      return row.maxSteps === null ? 'No budget left' : 'No budget left for its steps'
    case 'outOfTheMarket':
      return row.distancePct !== null && row.distancePct > 0 && words
        ? `${pct(row.distancePct)}% from ${words.zone}`
        : (words?.notCandidate ?? 'Rules not met today')
    case 'watching':
      if (row.aiScore !== undefined && row.aiScore !== null) return `Last score: ${row.aiScore}`
      if (words?.watching) return row.distancePct !== null ? fillN(words.watching, row.distancePct) : 'Rules not met today'
      // 0 means it is there or past it: the rules aren't met for another reason.
      return row.distancePct !== null && row.distancePct > 0 && words
        ? `${pct(row.distancePct)}% from ${words.zone}`
        : 'Rules not met today'
    case 'notCandidate':
      return words?.notCandidate ? fillN(words.notCandidate, row.distancePct) : 'Not a candidate yet'
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
  done: ['DONE', 'holding'],
  building: ['BUILDING', 'building'],
  outOfTheMarket: ['OUT OF THE MARKET', 'watching'],
  noBudget: ['WATCHING', 'watching'],
  watching: ['WATCHING', 'watching'],
  notCandidate: ['WATCHING', 'watching'],
  notEnoughData: ['WATCHING', 'watching'],
  notChecked: ['WATCHING', 'watching'],
}

/** The words of a stock's row on the strategy page (spec: Strategy page, one row per stock). */
export function stockLine(row: StockRow, mode: StrategyMode, currency: Currency, words: StockStatusWords | null): StockLine {
  const [pill, tone] = PILLS[row.status]
  return {
    pill,
    tone,
    detail: detailOf(row, words),
    result: row.result === null ? '–' : signedPct(row.result, 1),
    note: row.result === null ? 'No buys yet' : `${mode === 'live' ? 'You ' : ''}${money(row.invested, currency)} in`,
  }
}
