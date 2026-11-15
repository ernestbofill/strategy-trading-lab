import { FormMessage, TextField } from '../../components/Field'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { formatRate, shortDate, symbolOf } from '../../lib/format'
import { rateOn, type TradeForm } from './signalForm'
import styles from './TradeFields.module.css'

interface TradeFieldsProps {
  form: TradeForm
  set: (patch: Partial<TradeForm>) => void
  errors: Record<string, string>
  assetCurrency: string
  strategyCurrency: string
  /** Reference rates by day for this pair (asset currency per one unit of the strategy's). */
  rates: { date: string; rate: number }[]
  /** The earliest day (a signal's own day), and today. */
  minDate?: string
  maxDate: string
  priceHint?: string
  amountHint?: string
}

/**
 * A trade's fields as typed: day, price, amount or shares, fee and, between two currencies, the reference rate or the user's
 * own. Shared by answering a signal, logging a trade by hand and editing one, so all three read the same.
 */
export function TradeFields({ form, set, errors, assetCurrency, strategyCurrency, rates, minDate, maxDate, priceHint, amountHint }: TradeFieldsProps) {
  const t = useT()
  const reference = rateOn(rates, form.date)
  const rateUnit = t('signal.rate.unit', { asset: symbolOf(assetCurrency), strategy: symbolOf(strategyCurrency) })

  return (
    <>
      <TextField
        label={t('signal.field.date')}
        type="date"
        value={form.date}
        min={minDate}
        max={maxDate}
        onChange={(date) => set({ date })}
        error={errors.date}
      />
      <TextField
        label={t('signal.field.price', { currency: assetCurrency })}
        type="decimal"
        value={form.price}
        placeholder="0.00"
        onChange={(price) => set({ price })}
        hint={priceHint}
        error={errors.price}
      />
      {form.enteredAs === 'amount' ? (
        <TextField
          label={t('signal.field.amount', { currency: strategyCurrency })}
          type="decimal"
          value={form.amount}
          onChange={(amount) => set({ amount })}
          hint={amountHint}
          error={errors.amount}
          aside={<TextAction onClick={() => set({ enteredAs: 'shares' })}>{t('signal.field.sharesInstead')}</TextAction>}
        />
      ) : (
        <TextField
          label={t('signal.field.shares')}
          type="decimal"
          value={form.shares}
          onChange={(shares) => set({ shares })}
          error={errors.shares}
          aside={<TextAction onClick={() => set({ enteredAs: 'amount' })}>{t('signal.field.amountInstead')}</TextAction>}
        />
      )}
      <TextField
        label={t('signal.field.fee', { currency: strategyCurrency })}
        type="decimal"
        value={form.fee}
        placeholder="0.00"
        onChange={(fee) => set({ fee })}
        error={errors.fee}
      />
      {assetCurrency !== strategyCurrency &&
        (form.ownRate === null ? (
          <div className={styles.rate}>
            <p className={styles.plain}>
              {reference
                ? t('signal.rate.at', {
                    rate: `${symbolOf(assetCurrency)}${formatRate(reference.rate)}`,
                    unit: `${symbolOf(strategyCurrency)}1`,
                    date: shortDate(reference.date),
                  })
                : t('signal.rate.none')}
            </p>
            <TextAction onClick={() => set({ ownRate: reference ? String(reference.rate) : '' })}>{t('signal.rate.change')}</TextAction>
            {errors.fxRate && <FormMessage tone="error">{errors.fxRate}</FormMessage>}
          </div>
        ) : (
          <TextField
            label={t('signal.rate.own', { unit: rateUnit })}
            type="decimal"
            value={form.ownRate}
            onChange={(ownRate) => set({ ownRate })}
            error={errors.fxRate}
            aside={reference && <TextAction onClick={() => set({ ownRate: null })}>{t('signal.rate.reference')}</TextAction>}
          />
        ))}
    </>
  )
}
