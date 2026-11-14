import type { CheckGroup, CheckTexts, RuleCheck, StockRow } from '../../api'
import { exact, formatRate, shortDate } from '../../lib/format'

const minus = (s: string) => s.replace('-', '−')
const plain = (n: number | null | undefined) => (n === null || n === undefined ? '–' : minus(formatRate(n)))
const signed = (n: number | null | undefined) => (n === null || n === undefined ? '–' : `${n > 0 ? '+' : ''}${plain(n)}`)
const price = (n: number | null | undefined, currency: string) => (n === null || n === undefined ? '–' : exact(n, currency))

/** Converts pre-deploy `buyLine` checks (measured vs the buy line) into % above the recent low (`midLevel`). */
export function normalizeCheck(check: RuleCheck): RuleCheck {
  if (
    check.key === 'buyLine' &&
    check.endValue == null &&
    check.value !== null &&
    check.min !== null &&
    check.max !== null &&
    check.max <= check.min &&
    check.level !== null &&
    check.midLevel != null &&
    check.midLevel > 0
  ) {
    const approxPrice = check.level * (1 + check.value / 100)
    const rawAbove = Math.max(0, ((approxPrice - check.midLevel) / check.midLevel) * 100)
    // Because legacy `value` was rounded to 0.1%, a stock right at its low (`0%` above) rounds back to ~0.1%.
    const aboveLow = rawAbove < 0.15 ? 0 : Math.round(rawAbove * 10) / 10
    const maxAboveLow = Math.round(((1 + check.min / 100) * (1 + check.max / 100) - 1) * 1000) / 10
    return { ...check, value: aboveLow, max: maxAboveLow, endValue: check.max }
  }
  return check
}

/** Whether an unmet check has gone past its upper bound (window closed, fell past zone, or past chase limit). */
export const isPastMax = (raw: RuleCheck): boolean => {
  const check = normalizeCheck(raw)
  return (
    check.met === false &&
    check.value !== null &&
    check.max !== null &&
    check.value > check.max &&
    !(check.key === 'golden' && (check.subValue ?? 0) <= 0)
  )
}

/** A check's words with its numbers in: {value}, {+value} (signed), {min}, {max}, {level}, {midLevel}, {endLevel}, and {date}. */
export function fillCheck(text: string, raw: RuleCheck, currency: string): string {
  const check = normalizeCheck(raw)
  const out = text
    .replaceAll('{+value}', signed(check.value))
    .replaceAll('{value}', plain(check.value))
    .replaceAll('{+subValue}', signed(check.subValue))
    .replaceAll('{subValue}', plain(check.subValue))
    .replaceAll('{+endValue}', signed(check.endValue))
    .replaceAll('{endValue}', plain(check.endValue))
    .replaceAll('{min}', plain(check.min))
    .replaceAll('{max}', plain(check.max))
    .replaceAll('{level}', price(check.level, currency))
    .replaceAll('{midLevel}', price(check.midLevel, currency))
    .replaceAll('{endLevel}', price(check.endLevel, currency))
    .replaceAll('{date}', check.date ? shortDate(check.date) : '–')
  // Cleanly strip optional clauses when rendering pre-deploy stored evaluations that lack the new fields.
  return out
    .replace(/ Now –%[^.]*\./g, '')
    .replace(/ Ara –%[^.]*\./g, '')
    .replace(/ (?:since|on|des del|el) – \(/g, ' (')
    .replace(/\(– → –, /g, '(')
    .replace(/ \((?:up to |fins a )?–\)/g, '')
    .replace(/ to –\)/g, ')')
}

/**
 * Where the stock stands on one rule: the detail sentence, or the short one for the list; `past` when past `max`, `none`
 * when it has no value, or `tracking` (the detail) when it has none but follows a level.
 */
export function checkText(raw: RuleCheck, texts: CheckTexts, currency: string, kind: 'detail' | 'short'): string {
  const check = normalizeCheck(raw)
  const past = isPastMax(check) ? (kind === 'detail' ? texts.past : texts.pastShort) : null
  const tracking = kind === 'detail' && check.midLevel != null ? texts.tracking : null
  const noValue = tracking || texts.none
  const text = check.value === null && noValue ? noValue : (past ?? texts[kind])
  return fillCheck(text, check, currency)
}

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

/**
 * The strategy page's line for a stock from its checks: the first two rules of these groups it doesn't meet, in a few
 * words ("Volume 0.84× (needs 1.2×)"). Null when it has no such check or no words for it.
 */
export function unmetLine(row: StockRow, texts: Record<string, CheckTexts> | null | undefined, groups: CheckGroup[]): string | null {
  if (!texts) return null
  const unmet = row.checks.filter((c) => !c.preview && groups.includes(c.group) && c.met === false && texts[c.key])
  if (unmet.length === 0) return null
  const beaten = unmet.find((c) => c.key === 'beaten')
  const picked = beaten ? [beaten] : unmet.slice(0, 2)
  return capitalize(
    picked
      .map((c) => checkText(c, texts[c.key], row.currency, 'short'))
      .join(' · '),
  )
}
