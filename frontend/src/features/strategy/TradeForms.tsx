import { useState, type FormEvent, type ReactNode } from 'react'
import { api, InvalidFieldsError, InvalidValueError, type SignalTrade, type StrategyDetail, type StrategyTradeItem, type TradeSide } from '../../api'
import { FormMessage, SegmentedField, SelectField, type FormMessageState } from '../../components/Field'
import { useT } from '../../i18n/I18n'
import { ambiguousFields, tradeRequest, type TradeForm } from '../signal/signalForm'
import { TradeFields } from '../signal/TradeFields'
import styles from './SignalsAndTrades.module.css'

interface TradeFormCardProps {
  title: string
  initial: TradeForm
  assetCurrency: string
  strategyCurrency: string
  rates: { date: string; rate: number }[]
  minDate?: string
  today: string
  saveLabel: string
  failText: string
  onSubmit: (trade: SignalTrade) => Promise<unknown>
  onDone: () => void
  onCancel: () => void
  /** Fields above the trade's own (the stock and the side when logging by hand), with the errors the server gave them. */
  children?: (errors: Record<string, string>) => ReactNode
}

/** A trade typed on the strategy page: logged by hand or edited. Saves, maps the server's refusals to fields, or says why. */
function TradeFormCard(props: TradeFormCardProps) {
  const { title, initial, saveLabel, failText, onSubmit, onDone, onCancel, children } = props
  const t = useT()
  const [form, setForm] = useState(initial)
  const set = (patch: Partial<TradeForm>) => setForm((f) => ({ ...f, ...patch }))
  const [saving, setSaving] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)
  // Another stock in another currency: a rate typed for the old pair doesn't apply, so the reference rate comes back.
  const [pair, setPair] = useState(props.assetCurrency)
  if (pair !== props.assetCurrency) {
    setPair(props.assetCurrency)
    set({ ownRate: null })
    setErrors(({ fxRate: _dropped, ...others }) => others)
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (saving) return
    setErrors({})
    setMessage(null)
    const unclear = ambiguousFields(form, t('signal.error.ambiguous'))
    if (Object.keys(unclear).length > 0) return setErrors(unclear)
    setSaving(true)
    try {
      await onSubmit(tradeRequest(form))
      onDone()
    } catch (err) {
      if (err instanceof InvalidFieldsError) setErrors(err.fields)
      else setMessage({ tone: 'error', text: err instanceof InvalidValueError ? err.message : failText })
      setSaving(false)
    }
  }

  return (
    <form className={styles.formCard} onSubmit={(e) => void submit(e)}>
      <div className={styles.formTitle}>{title}</div>
      {children?.(errors)}
      <TradeFields
        form={form}
        set={set}
        errors={errors}
        assetCurrency={props.assetCurrency}
        strategyCurrency={props.strategyCurrency}
        rates={props.rates}
        minDate={props.minDate}
        maxDate={props.today}
      />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <div className={styles.formActions}>
        <button type="submit" className={styles.btnPrimary} disabled={saving}>
          {saving ? t('trades.saving') : saveLabel}
        </button>
        <button type="button" className={styles.btnSecondary} disabled={saving} onClick={onCancel}>
          {t('trades.cancel')}
        </button>
      </div>
    </form>
  )
}

const ratesFor = (s: StrategyDetail, currency: string) => s.rates?.find((r) => r.currency === currency)?.rates ?? []

/** "Log a trade by hand": any stock on the page, also one unticked with Edit list, which takes only a sale. */
export function ManualTradeForm({ s, onDone, onCancel }: { s: StrategyDetail; onDone: () => void; onCancel: () => void }) {
  const t = useT()
  const [assetId, setAssetId] = useState(s.stocks[0]?.assetId ?? '')
  const removed = (id: string) => s.stocks.some((st) => st.assetId === id && st.status === 'removed')
  const [side, setSide] = useState<TradeSide>(removed(assetId) ? 'sell' : 'buy')
  const pickAsset = (id: string) => {
    setAssetId(id)
    if (removed(id)) setSide('sell')
  }
  const assetCurrency = s.stocks.find((st) => st.assetId === assetId)?.currency ?? s.currency

  return (
    <TradeFormCard
      title={t('trades.logByHand')}
      initial={{ date: s.today, price: '', enteredAs: 'amount', amount: '', shares: '', fee: '', ownRate: null }}
      assetCurrency={assetCurrency}
      strategyCurrency={s.currency}
      rates={ratesFor(s, assetCurrency)}
      today={s.today}
      saveLabel={t('trades.saveTrade')}
      failText={t('trades.errorSave')}
      onSubmit={(trade) => api.logManualTrade(s.id, { ...trade, assetId, side })}
      onDone={onDone}
      onCancel={onCancel}
    >
      {(errors) => (
        <>
          <SelectField
            label={t('trades.stock')}
            value={assetId}
            error={errors.assetId}
            options={s.stocks.map((st) => ({ value: st.assetId, label: `${st.name} (${st.symbol})` }))}
            onChange={pickAsset}
          />
          <SegmentedField
            label={t('trades.side')}
            hideLabel
            value={side}
            onChange={(v) => setSide(v as TradeSide)}
            options={[
              ...(removed(assetId) ? [] : [{ value: 'buy', label: t('trades.buy') }]),
              { value: 'sell', label: t('trades.sale') },
            ]}
            error={errors.side}
          />
        </>
      )}
    </TradeFormCard>
  )
}

/** Editing a trade the user logged or answered: a signal's trade stays on or after the signal's day. */
export function EditTradeForm({ s, trade, onDone, onCancel }: { s: StrategyDetail; trade: StrategyTradeItem; onDone: () => void; onCancel: () => void }) {
  const t = useT()
  const minDate = trade.source === 'signal' && trade.signalId ? s.signalHistory?.find((sig) => sig.id === trade.signalId)?.signalDate : undefined

  return (
    <TradeFormCard
      title={t('trades.editTitle', { stock: trade.stockName, side: t(trade.side === 'buy' ? 'trades.buyTag' : 'trades.sellTag') })}
      initial={{
        date: trade.tradeDate,
        price: String(trade.price),
        enteredAs: trade.enteredAs,
        amount: String(trade.amount),
        shares: String(trade.shares),
        fee: trade.fee > 0 ? String(trade.fee) : '',
        ownRate: trade.fxSource === 'user' ? String(trade.fxRate) : null,
      }}
      assetCurrency={trade.assetCurrency}
      strategyCurrency={trade.strategyCurrency}
      rates={ratesFor(s, trade.assetCurrency)}
      minDate={minDate}
      today={s.today}
      saveLabel={t('trades.saveChanges')}
      failText={t('trades.errorSaveChanges')}
      onSubmit={(req) => api.updateTrade(trade.id, req)}
      onDone={onDone}
      onCancel={onCancel}
    />
  )
}
