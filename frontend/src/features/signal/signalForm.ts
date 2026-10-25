import type { SignalDetail, SignalTrade, SkipReason } from '../../api'
import type { MessageKey } from '../../i18n/messages/en'
import type { MessageParams } from '../../i18n/format'

/** "I did it" as typed: strings, so a field can be empty or half-typed. `ownRate` is null while the reference rate is used. */
export interface TradeForm {
  date: string
  price: string
  enteredAs: 'amount' | 'shares'
  amount: string
  shares: string
  fee: string
  ownRate: string | null
}

export const SKIP_REASON_KEYS: Record<SkipReason, MessageKey> = {
  priceRanAway: 'signal.skip.priceRanAway',
  didntTrustIt: 'signal.skip.didntTrustIt',
  noCash: 'signal.skip.noCash',
  other: 'signal.skip.other',
}

export const SKIP_REASONS: { id: SkipReason; label: string }[] = [
  { id: 'priceRanAway', label: 'Price ran away' },
  { id: 'didntTrustIt', label: "Didn't trust it" },
  { id: 'noCash', label: 'No cash' },
  { id: 'other', label: 'Other' },
]

export const skipReasons = (t?: (key: MessageKey) => string) =>
  SKIP_REASONS.map((r) => ({ id: r.id, label: t ? t(SKIP_REASON_KEYS[r.id]) : r.label }))

/** The form as the signal prefills it: the next trading day, its open when known, the step without the fee (or held shares for a sale), no fee. */
export const initialForm = (s: SignalDetail): TradeForm => {
  const sell = s.side === 'sell'
  return {
    date: s.prefill.date,
    price: s.prefill.price === null ? '' : String(s.prefill.price),
    enteredAs: sell ? 'shares' : 'amount',
    amount: s.prefill.amount === null ? '' : s.prefill.amount.toFixed(2),
    shares: s.prefill.shares == null ? '' : String(s.prefill.shares),
    fee: '',
    ownRate: null,
  }
}

/**
 * Whether a number can be read two ways: one comma or one dot, three digits after it and not "0" before ("1,250" and
 * "1.250" are 1250 or 1.25, by country). The screen asks instead of guessing.
 */
export const isAmbiguous = (text: string) => /^[1-9]\d{0,2}[,.]\d{3}$/.test(text.trim().replace(/\s/g, ''))

/**
 * A typed number; null when empty, not a number or ambiguous. The last of a dot and a comma is the decimal mark and the
 * other groups thousands ("1,250.50", "1.250,50"). Several commas, or several dots, group thousands ("12,345,678"). One
 * comma alone is the decimal comma ("98,80", "0,125").
 */
export function parseNumber(text: string): number | null {
  const clean = text.trim().replace(/\s/g, '')
  if (!clean || isAmbiguous(clean)) return null
  const commas = clean.split(',').length - 1
  const dots = clean.split('.').length - 1
  let plain: string
  if (commas && dots) plain = clean.lastIndexOf(',') > clean.lastIndexOf('.') ? clean.replace(/\./g, '').replace(',', '.') : clean.replace(/,/g, '')
  else if (commas > 1) plain = clean.replace(/,/g, '')
  else if (dots > 1) plain = clean.replace(/\./g, '')
  else plain = clean.replace(',', '.')
  const n = Number(plain)
  return Number.isFinite(n) ? n : null
}

/** The fields whose text could be read two ways, keyed as the server names them, with what to write instead. */
export function ambiguousFields(form: TradeForm, ask = 'Is that 1250 or 1.25? Write it as 1250 or 1.25.'): Record<string, string> {
  const errors: Record<string, string> = {}
  const check = (field: string, text: string | null) => text !== null && isAmbiguous(text) && (errors[field] = ask)
  check('price', form.price)
  check(form.enteredAs === 'amount' ? 'amount' : 'shares', form.enteredAs === 'amount' ? form.amount : form.shares)
  check('fee', form.fee)
  check('fxRate', form.ownRate)
  return errors
}

/** The reference rate the server uses for a day: the latest on or before it. */
export function rateOn(rates: SignalDetail['rates'], date: string): SignalDetail['rates'][number] | null {
  let found: SignalDetail['rates'][number] | null = null
  for (const r of rates) if (r.date <= date) found = r
  return found
}

/** What "Save the trade" sends. An empty or unreadable field is sent as null (0 for a price), so the server says which is wrong. */
export function tradeRequest(form: TradeForm): SignalTrade {
  const byAmount = form.enteredAs === 'amount'
  return {
    date: form.date || null,
    price: parseNumber(form.price) ?? 0,
    enteredAs: form.enteredAs,
    amount: byAmount ? parseNumber(form.amount) : null,
    shares: byAmount ? null : parseNumber(form.shares),
    fee: parseNumber(form.fee),
    fxSource: form.ownRate === null ? 'reference' : 'user',
    fxRate: form.ownRate === null ? null : parseNumber(form.ownRate),
  }
}

/** "step 3 of 5": the next signal's step after a skip, or null once the last step is reached (or the signal has none). */
export const nextStep = (s: SignalDetail, t?: (key: MessageKey, params?: MessageParams) => string) => {
  if (s.stepNo === null || s.maxSteps === null || s.stepNo >= s.maxSteps) return null
  return t ? t('signal.nextStep', { n: s.stepNo + 1, max: s.maxSteps }) : `step ${s.stepNo + 1} of ${s.maxSteps}`
}
