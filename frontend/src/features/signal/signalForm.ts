import type { SignalDetail, SignalTrade, SkipReason } from '../../api'

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

export const SKIP_REASONS: { id: SkipReason; label: string }[] = [
  { id: 'priceRanAway', label: 'Price ran away' },
  { id: 'didntTrustIt', label: "Didn't trust it" },
  { id: 'noCash', label: 'No cash' },
  { id: 'other', label: 'Other' },
]

/** The form as the signal prefills it: the next trading day, its open when known, the step without the fee, no fee. */
export const initialForm = (s: SignalDetail): TradeForm => ({
  date: s.prefill.date,
  price: s.prefill.price === null ? '' : String(s.prefill.price),
  enteredAs: 'amount',
  amount: s.prefill.amount === null ? '' : s.prefill.amount.toFixed(2),
  shares: '',
  fee: '',
  ownRate: null,
})

/**
 * A typed number; null when empty or not a number. With a dot, commas group thousands ("1,250.50"). Without one, a lone
 * comma before exactly three digits groups thousands too ("1,250" is 1250, as the app writes numbers); any other is the
 * decimal comma ("98,80").
 */
export function parseNumber(text: string): number | null {
  const clean = text.trim().replace(/\s/g, '')
  if (!clean) return null
  const grouped = clean.includes('.') || /^\d{1,3}(,\d{3})+$/.test(clean)
  const n = Number(grouped ? clean.replace(/,/g, '') : clean.replace(',', '.'))
  return Number.isFinite(n) ? n : null
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
export const nextStep = (s: SignalDetail) =>
  s.stepNo !== null && s.maxSteps !== null && s.stepNo < s.maxSteps ? `step ${s.stepNo + 1} of ${s.maxSteps}` : null
