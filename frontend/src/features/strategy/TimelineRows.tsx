import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { StrategyDetail, StrategySignalItem, StrategyTradeItem } from '../../api'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { exact, formatRate, formatShares, money, shortDate } from '../../lib/format'
import { signalPath } from '../dashboard/paths'
import { skipReasons } from '../signal/signalForm'
import type { PillTone } from './stockLine'
import { StockPill } from './StockPill'
import styles from './SignalsAndTrades.module.css'

/** A trade on the timeline, with Edit and Delete (after a confirm step) while it can still change. */
export function TradeRow({
  trade,
  editable,
  isDeleting,
  onStartEdit,
  onStartDelete,
  onCancelDelete,
  onConfirmDelete,
}: {
  trade: StrategyTradeItem
  /** Live and running: the user's own trade can be edited or deleted. */
  editable: boolean
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
        {editable && (
          <div className={styles.rowActions}>
            <TextAction onClick={onStartEdit}>{t('trades.edit')}</TextAction>
            <TextAction onClick={onStartDelete}>{t('trades.delete')}</TextAction>
          </div>
        )}
      </div>
      <div className={styles.rowDetails}>
        <span>
          <span className={styles.mono}>{t('trades.sharesAt', { shares: formatShares(trade.shares), price: exact(trade.price, trade.assetCurrency) })}</span>
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
            {' '}· <span className={styles.mono}>{formatRate(trade.fxRate)} {trade.assetCurrency}/{trade.strategyCurrency}</span>
          </span>
        )}
      </div>
    </div>
  )
}

export function SignalRow({ s, signal, live }: { s: StrategyDetail; signal: StrategySignalItem; live: boolean }) {
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

const PILLS: Record<StrategySignalItem['status'] | 'snoozed', [MessageKey, PillTone]> = {
  confirmed: ['trades.pill.did', 'done'],
  skipped: ['trades.pill.skipped', 'watching'],
  missed: ['trades.pill.missed', 'waiting'],
  snoozed: ['trades.pill.snoozed', 'signal'],
  waiting: ['trades.pill.waiting', 'waiting'],
}

/** What became of a signal: done, skipped, missed, snoozed or still waiting. */
function SignalPill({ signal }: { signal: StrategySignalItem }) {
  const t = useT()
  const [key, tone] = PILLS[signal.status === 'waiting' && signal.snoozedUntil ? 'snoozed' : signal.status]
  return <StockPill tone={tone}>{t(key)}</StockPill>
}
