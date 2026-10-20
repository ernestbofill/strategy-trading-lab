import { Link } from 'react-router-dom'
import type { StrategySummary, WaitingItem } from '../../api'
import { Sparkline } from '../../components/Sparkline'
import { Tag } from '../../components/Tag'
import { eur, shortDate, signedPct } from '../../lib/format'
import { strategyPath } from './paths'
import styles from './StrategyCard.module.css'

const toneOf = (pct: number) => (pct < 0 ? 'down' : 'up')

/** The dot before the mode tag that points at a card with something waiting. */
function WaitingDot({ item }: { item: WaitingItem }) {
  const label = item.kind === 'aiRound' ? 'AI round due' : 'Signal waiting'
  return <span role="img" aria-label={label} title={label} className={styles.dot} data-kind={item.kind} />
}

function Body({ s }: { s: StrategySummary }) {
  const { results, pastResult } = s
  if (results) {
    return (
      <div className={styles.results}>
        <div className={styles.figures}>
          <div className={styles.big} data-tone={toneOf(results.strategy)}>
            {signedPct(results.strategy, 1)}
          </div>
          <div className={styles.index}>Index fund: {signedPct(results.index, 1)}</div>
        </div>
        <span className={styles.chart} data-tone={toneOf(results.strategy)}>
          <Sparkline values={results.history} />
        </span>
      </div>
    )
  }
  if (pastResult) {
    return (
      <p className={styles.text}>
        {s.mode === 'live' ? 'No trades yet.' : 'No paper trades yet.'} In the past, {pastResult.years}, the template did{' '}
        <span className={styles.figure} data-tone={toneOf(pastResult.template)}>
          {signedPct(pastResult.template)}
        </span>{' '}
        vs <span className={styles.figure}>{signedPct(pastResult.index)}</span> for an index fund. Its worst year was{' '}
        <span className={styles.figure} data-tone={toneOf(pastResult.worstYear)}>
          {signedPct(pastResult.worstYear)}
        </span>
        .
      </p>
    )
  }
  return <p className={styles.text}>No results yet. They start after your first AI round.</p>
}

/** One strategy on the dashboard. The whole card links to the strategy page. */
export function StrategyCard({ strategy: s, today, waiting }: { strategy: StrategySummary; today: string; waiting?: WaitingItem }) {
  const live = s.mode === 'live'
  const since = s.startedOn === today ? 'started today' : `since ${shortDate(s.startedOn)}`
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
          {eur(s.used)} of {eur(s.budget)} used
        </div>
      </div>
    </Link>
  )
}
