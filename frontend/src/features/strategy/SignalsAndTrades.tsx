import { useState } from 'react'
import { api, type StrategyDetail, type StrategySignalItem, type StrategyTradeItem } from '../../api'
import { Section } from '../../components/Section'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { invalidate } from '../../lib/useLoadable'
import { dashboardCacheKey } from '../dashboard/useDashboard'
import { SignalRow, TradeRow } from './TimelineRows'
import { EditTradeForm, ManualTradeForm } from './TradeForms'
import { strategyCacheKey } from './useStrategy'
import styles from './SignalsAndTrades.module.css'

type TimelineItem =
  | { kind: 'trade'; trade: StrategyTradeItem; date: string; createdAt: string }
  | { kind: 'signal'; signal: StrategySignalItem; date: string; createdAt: string }

export function SignalsAndTrades({ s }: { s: StrategyDetail }) {
  const t = useT()
  const live = s.mode === 'live'
  // An ended strategy's history is final: no trades to log, edit or delete.
  const running = s.status === 'running'
  const editable = live && running

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
        editable ? (
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
                    editable={editable}
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
                {/* An ended strategy's signals can't be answered any more: only an answered one still opens. */}
                <SignalRow s={s} signal={signal} live={live && (running || signal.status === 'confirmed' || signal.status === 'skipped')} />
              </li>
            )
          })}
        </ul>
      )}
    </Section>
  )
}
