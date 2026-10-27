import { CURRENCIES } from '../../lib/format'
import type { Translate } from '../../i18n/I18n'

/** The currency choices: "€ EUR" over "Euro". */
export const currencyOptionsWith = (t: Translate) =>
  CURRENCIES.map((c) => ({
    value: c.code,
    label: `${c.symbol} ${c.code}`,
    detail: t(`account.currencyName.${c.code}` as 'account.currencyName.EUR'),
  }))
