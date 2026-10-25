import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  api,
  InvalidFieldsError,
  InvalidValueError,
  type ManualTradeRequest,
  type StrategyDetail,
  type StrategySignalItem,
  type StrategyTradeItem,
  type TradeSide,
  type UpdateTradeRequest,
} from '../../api'
import { FormMessage, SelectField, TextField, type FormMessageState } from '../../components/Field'
import { Section } from '../../components/Section'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { exact, money, shortDate, symbolOf } from '../../lib/format'
import { invalidate } from '../../lib/useLoadable'
import { dashboardCacheKey } from '../dashboard/useDashboard'
import { signalPath } from '../dashboard/paths'
import { ambiguousFields, parseNumber, rateOn, skipReasons, type TradeForm } from '../signal/signalForm'
import { strategyCacheKey } from './useStrategy'
import styles from './SignalsAndTrades.module.css'

type TimelineItem =
  | { kind: 'trade'; trade: StrategyTradeItem; date: string; createdAt: string }
  | { kind: 'signal'; signal: StrategySignalItem; date: string; createdAt: string }

export function SignalsAndTrades({ s }: { s: StrategyDetail }) {
  const t = useT()
  const live = s.mode === 'live'

  const [formMode, setFormMode] = useState<'add' | null>(null)
  const [editingTrade, setEditingTrade] = useState<StrategyTradeItem | null>(null)
  const [deletingTradeId, setDeletingTradeId] = useState<string | null>(null)

  // Timeline combining trades and signals, newest date first
  const items: TimelineItem[] = [
    ...(s.trades ?? []).map((t) => ({ kind: 'trade' as const, trade: t, date: t.tradeDate, createdAt: t.tradeDate })),
    ...(s.signalHistory ?? []).map((sig) => ({ kind: 'signal' as const, signal: sig, date: sig.signalDate, createdAt: sig.createdAt })),
  ].sort((a, b) => {
    const cmp = b.date.localeCompare(a.date)
    if (cmp !== 0) return cmp
    return b.createdAt.localeCompare(a.createdAt)
  })

  const refresh = () => {
    invalidate(strategyCacheKey(s.id))
    invalidate(dashboardCacheKey)
  }

  return (
    <Section
      title={t('trades.title')}
      aside={
        live ? (
          <TextAction
            onClick={() => {
              setEditingTrade(null)
              setDeletingTradeId(null)
              setFormMode(formMode === 'add' ? null : 'add')
            }}
          >
            {formMode === 'add' ? t('trades.closeForm') : t('trades.logByHand')}
          </TextAction>
        ) : undefined
      }
    >
      {formMode === 'add' && (
        <ManualTradeForm
          s={s}
          onDone={() => {
            setFormMode(null)
            refresh()
          }}
          onCancel={() => setFormMode(null)}
        />
      )}

      {items.length === 0 ? (
        <p className={styles.empty}>{t('trades.empty')}</p>
      ) : (
        <ul className={styles.list}>
          {items.map((item) => {
            if (item.kind === 'trade') {
              const { trade } = item
              if (editingTrade?.id === trade.id) {
                return (
                  <li key={`edit-${trade.id}`}>
                    <EditTradeForm
                      s={s}
                      trade={trade}
                      onDone={() => {
                        setEditingTrade(null)
                        refresh()
                      }}
                      onCancel={() => setEditingTrade(null)}
                    />
                  </li>
                )
              }
              return (
                <li key={`trade-${trade.id}`}>
                  <TradeRow
                    trade={trade}
                    live={live}
                    isDeleting={deletingTradeId === trade.id}
                    onStartEdit={() => {
                      setFormMode(null)
                      setDeletingTradeId(null)
                      setEditingTrade(trade)
                    }}
                    onStartDelete={() => setDeletingTradeId(trade.id)}
                    onCancelDelete={() => setDeletingTradeId(null)}
                    onConfirmDelete={async () => {
                      await api.deleteTrade(trade.id)
                      setDeletingTradeId(null)
                      refresh()
                    }}
                  />
                </li>
              )
            }

            const { signal } = item
            return (
              <li key={`sig-${signal.id}`}>
                <SignalRow s={s} signal={signal} live={live} />
              </li>
            )
          })}
        </ul>
      )}
    </Section>
  )
}

function TradeRow({
  trade,
  live,
  isDeleting,
  onStartEdit,
  onStartDelete,
  onCancelDelete,
  onConfirmDelete,
}: {
  trade: StrategyTradeItem
  live: boolean
  isDeleting: boolean
  onStartEdit: () => void
  onStartDelete: () => void
  onCancelDelete: () => void
  onConfirmDelete: () => Promise<void>
}) {
  const t = useT()
  const [busy, setBusy] = useState(false)

  if (isDeleting) {
    return (
      <div className={styles.confirmBox}>
        <p>{t('trades.deleteAsk')}</p>
        <div className={styles.confirmActions}>
          <button
            type="button"
            className={styles.btnDanger}
            disabled={busy}
            onClick={async () => {
              setBusy(true)
              try {
                await onConfirmDelete()
              } finally {
                setBusy(false)
              }
            }}
          >
            {busy ? t('trades.deleting') : t('trades.delete')}
          </button>
          <button type="button" className={styles.btnSecondary} disabled={busy} onClick={onCancelDelete}>
            {t('trades.cancel')}
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className={styles.row}>
      <div className={styles.rowTop}>
        <div className={styles.rowLeft}>
          <span className={styles.rowDate}>{shortDate(trade.tradeDate)}</span>
          <span className={styles.rowTitle}>{trade.stockName}</span>
          <Tag tone={trade.side === 'buy' ? 'live' : 'sim'}>{t(trade.side === 'buy' ? 'trades.buyTag' : 'trades.sellTag')}</Tag>
          {trade.source === 'manual' && <span className={styles.manualBadge}>{t('trades.manual')}</span>}
        </div>
        {live && (
          <div className={styles.rowActions}>
            <TextAction onClick={onStartEdit}>{t('trades.edit')}</TextAction>
            <TextAction onClick={onStartDelete}>{t('trades.delete')}</TextAction>
          </div>
        )}
      </div>
      <div className={styles.rowDetails}>
        <span>
          <span className={styles.mono}>{t('trades.sharesAt', { shares: trade.shares, price: exact(trade.price, trade.assetCurrency) })}</span>
        </span>
        <span>
          {' '}· <span className={styles.mono}>{money(trade.amount, trade.strategyCurrency)}</span>
        </span>
        {trade.fee > 0 && (
          <span>
            {' '}<span className={styles.mono}>{t('trades.fee', { fee: money(trade.fee, trade.strategyCurrency) })}</span>
          </span>
        )}
        {trade.assetCurrency !== trade.strategyCurrency && (
          <span>
            {' '}· <span className={styles.mono}>{trade.fxRate} {trade.assetCurrency}/{trade.strategyCurrency}</span>
          </span>
        )}
      </div>
    </div>
  )
}

function SignalRow({ s, signal, live }: { s: StrategyDetail; signal: StrategySignalItem; live: boolean }) {
  const t = useT()
  const content = (
    <>
      <div className={styles.rowTop}>
        <div className={styles.rowLeft}>
          <span className={styles.rowDate}>{shortDate(signal.signalDate)}</span>
          <span className={styles.rowTitle}>{signal.stockName}</span>
          <span className={styles.signalType}>
            {t(signal.side === 'buy' ? 'trades.buySignal' : 'trades.sellSignal')}
            {signal.stepNo !== null && signal.maxSteps !== null ? ` · ${t('trades.stepOf', { n: signal.stepNo, max: signal.maxSteps })}` : ''}
          </span>
        </div>
        <SignalPill signal={signal} />
      </div>
      <div className={styles.rowDetails}>
        <span>
          <span className={styles.mono}>{t('trades.close', { price: exact(signal.closePrice, signal.assetCurrency) })}</span>
        </span>
        {signal.status === 'skipped' && signal.skipReason && (
          <span> · {skipReasons(t).find((r) => r.id === signal.skipReason)?.label ?? signal.skipReason}</span>
        )}
        {signal.status === 'waiting' && signal.snoozedUntil && <span> · {t('trades.remindOn', { date: shortDate(signal.snoozedUntil) })}</span>}
        {signal.status === 'missed' && signal.endedOn && <span> · {t('trades.endedOn', { date: shortDate(signal.endedOn) })}</span>}
        {signal.reason && <span className={styles.signalReason}> · {signal.reason}</span>}
      </div>
    </>
  )

  if (live) {
    return (
      <Link to={signalPath(s.id, signal.id)} className={styles.row}>
        {content}
      </Link>
    )
  }

  return <div className={styles.row}>{content}</div>
}

function SignalPill({ signal }: { signal: StrategySignalItem }) {
  const t = useT()
  if (signal.status === 'confirmed') {
    return (
      <span className={styles.pill} data-tone="holding">
        {t('trades.pill.did')}
      </span>
    )
  }
  if (signal.status === 'skipped') {
    return (
      <span className={styles.pill} data-tone="watching">
        {t('trades.pill.skipped')}
      </span>
    )
  }
  if (signal.status === 'missed') {
    return (
      <span className={styles.pill} data-tone="waiting">
        {t('trades.pill.missed')}
      </span>
    )
  }
  if (signal.snoozedUntil) {
    return (
      <span className={styles.pill} data-tone="signal">
        {t('trades.pill.snoozed')}
      </span>
    )
  }
  return (
    <span className={styles.pill} data-tone="waiting">
      {t('trades.pill.waiting')}
    </span>
  )
}

function ManualTradeForm({ s, onDone, onCancel }: { s: StrategyDetail; onDone: () => void; onCancel: () => void }) {
  const t = useT()
  const [assetId, setAssetId] = useState(s.stocks[0]?.assetId ?? '')
  const [side, setSide] = useState<TradeSide>('buy')
  const [date, setDate] = useState(s.today)
  const [price, setPrice] = useState('')
  const [enteredAs, setEnteredAs] = useState<'amount' | 'shares'>('amount')
  const [amount, setAmount] = useState('')
  const [shares, setShares] = useState('')
  const [fee, setFee] = useState('')
  const [ownRate, setOwnRate] = useState<string | null>(null)

  const [saving, setSaving] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)

  const assetCurrency = (s.trades?.find((t) => t.assetId === assetId)?.assetCurrency
    ?? s.signalHistory?.find((sig) => sig.assetId === assetId)?.assetCurrency
    ?? s.currency)

  const otherCurrency = assetCurrency !== s.currency
  const currencyRates = s.rates?.find((r) => r.currency === assetCurrency)?.rates ?? []
  const refRate = rateOn(currencyRates, date)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (saving) return
    setSaving(true)
    setErrors({})
    setMessage(null)

    const form: TradeForm = {
      date,
      price,
      enteredAs,
      amount,
      shares,
      fee,
      ownRate,
    }
    const unclear = ambiguousFields(form, t('signal.error.ambiguous'))
    if (Object.keys(unclear).length > 0) {
      setErrors(unclear)
      setSaving(false)
      return
    }

    try {
      const byAmount = enteredAs === 'amount'
      const req: ManualTradeRequest = {
        assetId,
        side,
        date: date || null,
        price: parseNumber(price) ?? 0,
        enteredAs,
        amount: byAmount ? parseNumber(amount) : null,
        shares: byAmount ? null : parseNumber(shares),
        fee: parseNumber(fee),
        fxSource: ownRate === null ? 'reference' : 'user',
        fxRate: ownRate === null ? null : parseNumber(ownRate),
      }
      await api.logManualTrade(s.id, req)
      onDone()
    } catch (err) {
      if (err instanceof InvalidFieldsError) {
        setErrors(err.fields)
      } else {
        setMessage({ tone: 'error', text: err instanceof InvalidValueError ? err.message : t('trades.errorSave') })
      }
      setSaving(false)
    }
  }

  return (
    <form className={styles.formCard} onSubmit={(e) => void handleSubmit(e)}>
      <div className={styles.formTitle}>{t('trades.logByHand')}</div>

      <SelectField
        label={t('trades.stock')}
        value={assetId}
        error={errors.assetId}
        options={s.stocks.map((st) => ({ value: st.assetId, label: `${st.name} (${st.symbol})` }))}
        onChange={setAssetId}
      />

      <div className={styles.sideToggle}>
        <button
          type="button"
          className={side === 'buy' ? styles.sideActive : styles.sideBtn}
          onClick={() => setSide('buy')}
        >
          {t('trades.buy')}
        </button>
        <button
          type="button"
          className={side === 'sell' ? styles.sideActive : styles.sideBtn}
          onClick={() => setSide('sell')}
        >
          {t('trades.sale')}
        </button>
      </div>

      <TextField
        label={t('signal.field.date')}
        type="date"
        value={date}
        max={s.today}
        error={errors.date}
        onChange={setDate}
      />

      <TextField
        label={t('signal.field.price', { currency: assetCurrency })}
        value={price}
        placeholder="0.00"
        error={errors.price}
        onChange={setPrice}
      />

      <div className={styles.toggleAction}>
        <TextAction onClick={() => setEnteredAs(enteredAs === 'amount' ? 'shares' : 'amount')}>
          {enteredAs === 'amount' ? t('trades.enterShares') : t('trades.enterAmount')}
        </TextAction>
      </div>

      {enteredAs === 'amount' ? (
        <TextField
          label={t('signal.field.amount', { currency: s.currency })}
          value={amount}
          placeholder="100.00"
          error={errors.amount}
          onChange={setAmount}
        />
      ) : (
        <TextField
          label={t('signal.field.shares')}
          value={shares}
          placeholder="1.00"
          error={errors.shares}
          onChange={setShares}
        />
      )}

      <TextField
        label={t('signal.field.fee', { currency: s.currency })}
        value={fee}
        placeholder="0.00"
        error={errors.fee}
        onChange={setFee}
      />

      {otherCurrency && (
        <div className={styles.rateLine}>
          {ownRate === null && refRate ? (
            <>
              <span>
                {t('signal.rate.at', {
                  rate: refRate.rate,
                  unit: t('signal.rate.unit', { asset: assetCurrency, strategy: symbolOf(s.currency) }),
                  date: shortDate(refRate.date),
                })}
              </span>
              <TextAction onClick={() => setOwnRate(String(refRate.rate))}>{t('signal.rate.change')}</TextAction>
            </>
          ) : (
            <>
              <TextField
                label={t('signal.rate.own', { unit: t('signal.rate.unit', { asset: assetCurrency, strategy: symbolOf(s.currency) }) })}
                value={ownRate ?? ''}
                error={errors.fxRate}
                onChange={setOwnRate}
              />
              {refRate && <TextAction onClick={() => setOwnRate(null)}>{t('trades.useReference')}</TextAction>}
            </>
          )}
        </div>
      )}

      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}

      <div className={styles.formActions}>
        <button type="submit" className={styles.btnPrimary} disabled={saving}>
          {saving ? t('trades.saving') : t('trades.saveTrade')}
        </button>
        <button type="button" className={styles.btnSecondary} disabled={saving} onClick={onCancel}>
          {t('trades.cancel')}
        </button>
      </div>
    </form>
  )
}

function EditTradeForm({
  s,
  trade,
  onDone,
  onCancel,
}: {
  s: StrategyDetail
  trade: StrategyTradeItem
  onDone: () => void
  onCancel: () => void
}) {
  const t = useT()
  const [date, setDate] = useState(trade.tradeDate)
  const [price, setPrice] = useState(String(trade.price))
  const [enteredAs, setEnteredAs] = useState<'amount' | 'shares'>(trade.enteredAs)
  const [amount, setAmount] = useState(String(trade.amount))
  const [shares, setShares] = useState(String(trade.shares))
  const [fee, setFee] = useState(trade.fee > 0 ? String(trade.fee) : '')
  const [ownRate, setOwnRate] = useState<string | null>(trade.fxSource === 'user' ? String(trade.fxRate) : null)

  const [saving, setSaving] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)

  const otherCurrency = trade.assetCurrency !== trade.strategyCurrency
  const currencyRates = s.rates?.find((r) => r.currency === trade.assetCurrency)?.rates ?? []
  const refRate = rateOn(currencyRates, date)

  const minDate = trade.source === 'signal' && trade.signalId
    ? (s.signalHistory?.find((sig) => sig.id === trade.signalId)?.signalDate ?? undefined)
    : undefined

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (saving) return
    setSaving(true)
    setErrors({})
    setMessage(null)

    const form: TradeForm = {
      date,
      price,
      enteredAs,
      amount,
      shares,
      fee,
      ownRate,
    }
    const unclear = ambiguousFields(form, t('signal.error.ambiguous'))
    if (Object.keys(unclear).length > 0) {
      setErrors(unclear)
      setSaving(false)
      return
    }

    try {
      const byAmount = enteredAs === 'amount'
      const req: UpdateTradeRequest = {
        date: date || null,
        price: parseNumber(price) ?? 0,
        enteredAs,
        amount: byAmount ? parseNumber(amount) : null,
        shares: byAmount ? null : parseNumber(shares),
        fee: parseNumber(fee),
        fxSource: ownRate === null ? 'reference' : 'user',
        fxRate: ownRate === null ? null : parseNumber(ownRate),
      }
      await api.updateTrade(trade.id, req)
      onDone()
    } catch (err) {
      if (err instanceof InvalidFieldsError) {
        setErrors(err.fields)
      } else {
        setMessage({ tone: 'error', text: err instanceof InvalidValueError ? err.message : t('trades.errorSaveChanges') })
      }
      setSaving(false)
    }
  }

  return (
    <form className={styles.formCard} onSubmit={(e) => void handleSubmit(e)}>
      <div className={styles.formTitle}>
        {t('trades.editTitle', { stock: trade.stockName, side: t(trade.side === 'buy' ? 'trades.buyTag' : 'trades.sellTag') })}
      </div>

      <TextField
        label={t('signal.field.date')}
        type="date"
        value={date}
        min={minDate}
        max={s.today}
        error={errors.date}
        onChange={setDate}
      />

      <TextField
        label={t('signal.field.price', { currency: trade.assetCurrency })}
        value={price}
        placeholder="0.00"
        error={errors.price}
        onChange={setPrice}
      />

      <div className={styles.toggleAction}>
        <TextAction onClick={() => setEnteredAs(enteredAs === 'amount' ? 'shares' : 'amount')}>
          {enteredAs === 'amount' ? t('trades.enterShares') : t('trades.enterAmount')}
        </TextAction>
      </div>

      {enteredAs === 'amount' ? (
        <TextField
          label={t('signal.field.amount', { currency: trade.strategyCurrency })}
          value={amount}
          placeholder="100.00"
          error={errors.amount}
          onChange={setAmount}
        />
      ) : (
        <TextField
          label={t('signal.field.shares')}
          value={shares}
          placeholder="1.00"
          error={errors.shares}
          onChange={setShares}
        />
      )}

      <TextField
        label={t('signal.field.fee', { currency: trade.strategyCurrency })}
        value={fee}
        placeholder="0.00"
        error={errors.fee}
        onChange={setFee}
      />

      {otherCurrency && (
        <div className={styles.rateLine}>
          {ownRate === null && refRate ? (
            <>
              <span>
                {t('signal.rate.at', {
                  rate: refRate.rate,
                  unit: t('signal.rate.unit', { asset: trade.assetCurrency, strategy: symbolOf(trade.strategyCurrency) }),
                  date: shortDate(refRate.date),
                })}
              </span>
              <TextAction onClick={() => setOwnRate(String(refRate.rate))}>{t('signal.rate.change')}</TextAction>
            </>
          ) : (
            <>
              <TextField
                label={t('signal.rate.own', { unit: t('signal.rate.unit', { asset: trade.assetCurrency, strategy: symbolOf(trade.strategyCurrency) }) })}
                value={ownRate ?? ''}
                error={errors.fxRate}
                onChange={setOwnRate}
              />
              {refRate && <TextAction onClick={() => setOwnRate(null)}>{t('trades.useReference')}</TextAction>}
            </>
          )}
        </div>
      )}

      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}

      <div className={styles.formActions}>
        <button type="submit" className={styles.btnPrimary} disabled={saving}>
          {saving ? t('trades.saving') : t('trades.saveChanges')}
        </button>
        <button type="button" className={styles.btnSecondary} disabled={saving} onClick={onCancel}>
          {t('trades.cancel')}
        </button>
      </div>
    </form>
  )
}
