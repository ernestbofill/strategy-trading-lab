import type { CheckGroup, CheckTexts, RuleCheck, StockRow } from '../../api'
import { exact, formatRate } from '../../lib/format'

const minus = (s: string) => s.replace('-', '−')
const plain = (n: number | null) => (n === null ? '–' : minus(formatRate(n)))
const signed = (n: number | null) => (n === null ? '–' : `${n > 0 ? '+' : ''}${plain(n)}`)

/** A check's words with its numbers in: {value}, {+value} (signed), {min}, {max}, and {level} (a price in the stock's currency). */
export function fillCheck(text: string, check: RuleCheck, currency: string): string {
  return text
    .replaceAll('{+value}', signed(check.value))
    .replaceAll('{value}', plain(check.value))
    .replaceAll('{min}', plain(check.min))
    .replaceAll('{max}', plain(check.max))
    .replaceAll('{level}', check.level === null ? '–' : exact(check.level, currency))
}

/** Where the stock stands on one rule: the detail sentence, or the short one for the list; `none` when it has no value. */
export function checkText(check: RuleCheck, texts: CheckTexts, currency: string, kind: 'detail' | 'short'): string {
  const text = check.value === null && texts.none ? texts.none : texts[kind]
  return fillCheck(text, check, currency)
}

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

/**
 * The strategy page's line for a stock from its checks: the first two rules of these groups it doesn't meet, in a few
 * words ("Volume 0.84× (needs 1.2×)"). Null when it has no such check or no words for it.
 */
export function unmetLine(row: StockRow, texts: Record<string, CheckTexts> | null | undefined, groups: CheckGroup[]): string | null {
  if (!texts) return null
  const unmet = row.checks.filter((c) => groups.includes(c.group) && c.met === false && texts[c.key])
  if (unmet.length === 0) return null
  return capitalize(
    unmet
      .slice(0, 2)
      .map((c) => checkText(c, texts[c.key], row.currency, 'short'))
      .join(' · '),
  )
}
