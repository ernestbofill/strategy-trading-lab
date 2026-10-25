import { CURRENCIES } from '../../lib/format'
import { tNow, type Translate } from '../../i18n/I18n'

/** The currency choices for a select: "EUR · Euro". */
export const currencyOptionsWith = (t: Translate) =>
  CURRENCIES.map((c) => ({
    value: c.code,
    label: t('account.currencyOption', {
      code: c.code,
      name: t(`account.currencyName.${c.code}` as 'account.currencyName.EUR'),
    }),
  }))

export const currencyOptions = CURRENCIES.map((c) => ({
  value: c.code,
  label: tNow('account.currencyOption', {
    code: c.code,
    name: tNow(`account.currencyName.${c.code}` as 'account.currencyName.EUR'),
  }),
}))
