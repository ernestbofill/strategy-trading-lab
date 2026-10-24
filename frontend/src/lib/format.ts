import type { Currency } from '../api/types'

const amountFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 })

/** The currencies a user can pick, matching the API's `AccountRules.Currencies`; the first is the default. */
export const CURRENCIES: { code: Currency; symbol: string; name: string }[] = [
  { code: 'EUR', symbol: '€', name: 'Euro' },
  { code: 'USD', symbol: '$', name: 'US dollar' },
  { code: 'GBP', symbol: '£', name: 'British pound' },
]

/** "Euro", "US dollar", "British pound". */
export const currencyName = (currency: Currency) => CURRENCIES.find((c) => c.code === currency)?.name ?? currency

/** €3,000 / $3,000 / £3,000: an amount in a strategy's or an account's currency. */
export const money = (n: number, currency: Currency) =>
  `${CURRENCIES.find((c) => c.code === currency)?.symbol ?? ''}${amountFormat.format(n)}`

/** €3,000: for what was tested in euro (a template's backtest). */
export const eur = (n: number) => money(n, 'EUR')

/** +12% / −8% with a true minus sign, so the sign carries the meaning, not only the colour. */
export const signedPct = (n: number, digits?: number) => {
  const abs = Math.abs(n)
  return `${n > 0 ? '+' : n < 0 ? '−' : ''}${digits === undefined ? abs : abs.toFixed(digits)}%`
}

/** Local calendar date as "2026-10-13". */
export const isoDate = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

const shortDateFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })

/** "Oct 13", from a local date like "2026-10-13". */
export const shortDate = (iso: string) => {
  const [y, m, d] = iso.split('-').map(Number)
  return shortDateFormat.format(new Date(y, m - 1, d))
}

/** `signedPct`, or "–" when the figure isn't known (an index fund without prices). */
export const signedPctOrDash = (n: number | null, digits?: number) => (n === null ? '–' : signedPct(n, digits))
