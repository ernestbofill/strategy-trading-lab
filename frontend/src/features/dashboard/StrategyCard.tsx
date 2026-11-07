import { Link } from 'react-router-dom'
import type { StockCounts, StrategySummary, WaitingItem } from '../../api'
import { Tag } from '../../components/Tag'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { money, pctTone, shortDate, signedPct, signedPctOrDash } from '../../lib/format'
import { strategyPath } from './paths'
import styles from './StrategyCard.module.css'

/** The dot before the mode tag that points at a card with something waiting. */
function WaitingDot({ item }: { item: WaitingItem }) {
  const t = useT()
  const label = item.kind === 'aiRound' ? t('dashboard.dot.aiRound') : t('dashboard.dot.signal')
  return <span role="img" aria-label={label} title={label} className={styles.dot} data-kind={item.kind} />
}

/** The pills counted on the card, most pressing first: the count's words, and the one fund's state. */
const STATES: [keyof StockCounts, MessageKey, MessageKey][] = [
  ['signals', 'dashboard.state.signals', 'dashboard.fund.signal'],
  ['close', 'dashboard.state.close', 'dashboard.fund.close'],
  ['candidates', 'dashboard.state.candidates', 'dashboard.fund.candidate'],
  ['holding', 'dashboard.state.holding', 'dashboard.fund.holding'],
  ['notCandidates', 'dashboard.state.notCandidates', 'dashboard.fund.notCandidate'],
]

/** Where the stocks stand: "1 getting close · 6 candidates", or the fund's own state ("CSPX: candidate"). */
function States({ stocks, fund }: { stocks: StockCounts; fund: string | null }) {
  const t = useT()
  const shown = STATES.filter(([key]) => stocks[key] > 0)
  if (shown.length === 0) return null
  return (
    <ul className={styles.states}>
      {fund && shown.length === 1 ? (
        <li data-state={shown[0][0]}>{t(shown[0][2], { fund })}</li>
      ) : (
        shown.map(([key, words]) => (
          <li key={key} data-state={key}>
            {t(words, { n: stocks[key] })}
          </li>
        ))
      )}
    </ul>
  )
}

/** One strategy on the dashboard: its gain since it started next to the S&P 500 fund's. The whole card links to the strategy page. */
export function StrategyCard({ strategy: s, today, waiting }: { strategy: StrategySummary; today: string; waiting?: WaitingItem }) {
  const t = useT()
  const live = s.mode === 'live'
  const since = s.endedOn
    ? t('dashboard.card.endedOn', { date: shortDate(s.endedOn) })
    : s.startedOn === today
      ? t('strategy.startedToday')
      : t('strategy.since', { date: shortDate(s.startedOn) })
  // Nothing bought yet: the money hasn't moved. Bought but not yet priced: unknown.
  const gain = s.results ? s.results.strategy : s.used > 0 ? null : 0
  const index = s.results ? s.results.index : s.indexSinceStart
  return (
    <Link to={strategyPath(s.id)} className={styles.card}>
      <div className={styles.head}>
        <div className={styles.titles}>
          <div className={styles.name}>{s.name}</div>
          <div className={styles.sub}>
            <em>{s.templateName}</em>
            {s.fund && <> · <span className={styles.fund}>{s.fund}</span></>} · {since}
          </div>
        </div>
        <span className={styles.badges}>
          {waiting && <WaitingDot item={waiting} />}
          {s.status === 'ended' && <Tag tone="ended">{t('dashboard.ended')}</Tag>}
          <Tag tone={live ? 'live' : 'sim'}>{live ? 'LIVE' : 'SIM'}</Tag>
        </span>
      </div>
      <div className={styles.figures}>
        <div className={styles.big} data-tone={pctTone(gain)}>
          {gain === null ? '–' : signedPct(gain, 1)}
        </div>
        <div className={styles.index}>{t('dashboard.card.index', { pct: signedPctOrDash(index, 1) })}</div>
      </div>
      {s.stocks && <States stocks={s.stocks} fund={s.fund} />}
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
