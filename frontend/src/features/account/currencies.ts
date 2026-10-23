import { CURRENCIES } from '../../lib/format'

/** The currency choices for a select: "EUR · Euro". */
export const currencyOptions = CURRENCIES.map((c) => ({ value: c.code, label: `${c.code} · ${c.name}` }))
