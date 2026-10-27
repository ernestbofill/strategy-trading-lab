import type { Currency, Locale } from '../api/types'
import { currentLocale } from '../i18n/locale'

const amountFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 })

/** The currencies a user can pick, matching the API's `AccountRules.Currencies`; the first is the default. */
export const CURRENCIES: { code: Currency; symbol: string; name: string }[] = [
  { code: 'EUR', symbol: '€', name: 'Euro' },
  { code: 'USD', symbol: '$', name: 'US dollar' },
  { code: 'GBP', symbol: '£', name: 'British pound' },
]

/** "Euro", "US dollar", "British pound". */
export const currencyName = (currency: Currency) => CURRENCIES.find((c) => c.code === currency)?.name ?? currency

/** "€", "$", "£", or the code itself for a currency the user can't pick ("JPY"). */
export const symbolOf = (currency: string) => CURRENCIES.find((c) => c.code === currency)?.symbol ?? currency

/** €3,000 / $3,000 / £3,000: an amount in a strategy's or an account's currency. */
export const money = (n: number, currency: Currency) => `${symbolOf(currency)}${amountFormat.format(n)}`

const sharesFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 })

/** 2.609 / 12 / 1,250.5: a number of shares, to at most 3 decimals (the stored value keeps its precision). */
export const formatShares = (n: number) => sharesFormat.format(n)

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

const shortDateEn = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })
const shortDateCa = new Intl.DateTimeFormat('ca-ES', { month: 'short', day: 'numeric' })

/** "Oct 13" (or "13 d’oct." in ca), from a local date like "2026-10-13". */
export const shortDate = (iso: string, locale?: Locale) => {
  const [y, m, d] = iso.split('-').map(Number)
  const loc = locale ?? currentLocale()
  const format = loc === 'ca' ? shortDateCa : shortDateEn
  return format.format(new Date(y, m - 1, d))
}

/** `signedPct`, or "–" when the figure isn't known (an index fund without prices). */
export const signedPctOrDash = (n: number | null, digits?: number) => (n === null ? '–' : signedPct(n, digits))

/** The colour of a gain: up, down, or none for zero or an unknown figure. */
export const pctTone = (n: number | null) => (n === null || n === 0 ? undefined : n < 0 ? 'down' : 'up')

const centsFormat = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

/** €98.80 / $187.50 / JPY 1,234.00: an amount or a price to the cent, in any currency (a stock's may be one the user can't pick). */
export const exact = (n: number, currency: string) => {
  const symbol = symbolOf(currency)
  return symbol !== currency ? `${symbol}${centsFormat.format(n)}` : `${currency} ${centsFormat.format(n)}`
}
