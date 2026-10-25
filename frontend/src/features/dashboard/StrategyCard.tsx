import { Link } from 'react-router-dom'
import type { StrategySummary, WaitingItem } from '../../api'
import { Sparkline } from '../../components/Sparkline'
import { Tag } from '../../components/Tag'
import { useT } from '../../i18n/I18n'
import { money, pctTone, shortDate, signedPct, signedPctOrDash } from '../../lib/format'
import { strategyPath } from './paths'
import styles from './StrategyCard.module.css'

/** The dot before the mode tag that points at a card with something waiting. */
function WaitingDot({ item }: { item: WaitingItem }) {
  const t = useT()
  const label = item.kind === 'aiRound' ? t('dashboard.dot.aiRound') : t('dashboard.dot.signal')
  return <span role="img" aria-label={label} title={label} className={styles.dot} data-kind={item.kind} />
}

function Body({ s }: { s: StrategySummary }) {
  const t = useT()
  const { results, pastResult } = s
  if (results) {
    return (
      <div className={styles.results}>
        <div className={styles.figures}>
          <div className={styles.big} data-tone={pctTone(results.strategy)}>
            {signedPct(results.strategy, 1)}
          </div>
          <div className={styles.index}>{t('dashboard.card.index', { pct: signedPctOrDash(results.index, 1) })}</div>
          {pastResult && (
            <div className={styles.index}>{t('dashboard.card.worstYear', { pct: signedPct(pastResult.worstYear) })}</div>
          )}
        </div>
        <span className={styles.chart} data-tone={pctTone(results.strategy)}>
          <Sparkline values={results.history} />
        </span>
      </div>
    )
  }
  if (s.used > 0) return <p className={styles.text}>{t('dashboard.card.noResultsBudget')}</p>
  if (pastResult) {
    const noTrades = s.mode === 'live' ? t('dashboard.card.noTradesLive') : t('dashboard.card.noTradesSim')
    return (
      <p className={styles.text}>
        {noTrades} {t('dashboard.card.pastBefore', { years: pastResult.years })}{' '}
        <span className={styles.figure} data-tone={pctTone(pastResult.template)}>
          {signedPct(pastResult.template)}
        </span>{' '}
        {t('dashboard.card.pastVs')} <span className={styles.figure}>{signedPct(pastResult.index)}</span>{' '}
        {t('dashboard.card.pastAfter')}{' '}
        <span className={styles.figure} data-tone={pctTone(pastResult.worstYear)}>
          {signedPct(pastResult.worstYear)}
        </span>
        .{s.templateKey === 'bear' && ` ${t('dashboard.card.bearRare')}`}
      </p>
    )
  }
  return <p className={styles.text}>{t('dashboard.card.noResultsAi')}</p>
}

/** One strategy on the dashboard. The whole card links to the strategy page. */
export function StrategyCard({ strategy: s, today, waiting }: { strategy: StrategySummary; today: string; waiting?: WaitingItem }) {
  const t = useT()
  const live = s.mode === 'live'
  const since = s.startedOn === today ? t('strategy.startedToday') : t('strategy.since', { date: shortDate(s.startedOn) })
  return (
    <Link to={strategyPath(s.id)} className={styles.card}>
      <div className={styles.head}>
        <div className={styles.titles}>
          <div className={styles.name}>{s.name}</div>
          <div className={styles.sub}>
            <em>{s.templateName}</em> · {since}
          </div>
        </div>
        <span className={styles.badges}>
          {waiting && <WaitingDot item={waiting} />}
          <Tag tone={live ? 'live' : 'sim'}>{live ? 'LIVE' : 'SIM'}</Tag>
        </span>
      </div>
      <Body s={s} />
      <div className={styles.budget}>
        <div className={styles.bar} aria-hidden="true" data-mode={s.mode}>
          <div className={styles.fill} style={{ width: `${Math.min(100, (s.used / s.budget) * 100)}%` }} />
        </div>
        <div className={styles.used}>
          {t('dashboard.card.budgetUsed', { used: money(s.used, s.currency), budget: money(s.budget, s.currency) })}
        </div>
      </div>
    </Link>
  )
}
