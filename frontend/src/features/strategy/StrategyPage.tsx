import { Link, useParams } from 'react-router-dom'
import type { StrategyDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { DISCLAIMER, PAST_RESULTS_CAPTION } from '../../lib/copy'
import { money, pctTone, shortDate, signedPct, signedPctOrDash } from '../../lib/format'
import { signalPath } from '../dashboard/paths'
import { dashboardPath } from '../new-strategy/paths'
import { ResultsChart } from './ResultsChart'
import styles from './StrategyPage.module.css'
import { stockLine } from './stockLine'
import { useStrategy } from './useStrategy'

function Figure({ label, value, highlight }: { label: string; value: number | null; highlight?: boolean }) {
  return (
    <div className={styles.figure}>
      <div className={styles.figureLabel}>{label}</div>
      <div className={styles.figureValue} data-tone={highlight ? pctTone(value) : undefined}>
        {signedPctOrDash(value, 1)}
      </div>
    </div>
  )
}

/** The summary box: results, chart, budget used and (live) how the signals were answered. */
function Summary({ s }: { s: StrategyDetail }) {
  const live = s.mode === 'live'
  const { results, pastResult, budget, signals } = s
  return (
    <div className={styles.summary}>
      {results ? (
        <>
          <div className={styles.figures} data-count={live ? 3 : 2}>
            <Figure label={live ? 'You' : 'Since start'} value={results.you} highlight />
            {live && <Figure label="The rules alone" value={results.rules} />}
            <Figure label="Index fund" value={results.index} />
          </div>
          <ResultsChart history={results.history} live={live} />
          {pastResult && (
            // The strategy has no worst year of its own until it has run a year: the template's stands in.
            <p className={styles.worst}>
              The template's worst year, {pastResult.years}: <span className={styles.mono}>{signedPct(pastResult.worstYear)}</span>
            </p>
          )}
        </>
      ) : (
        <p className={styles.text}>
          {budget.used > 0 ? 'Results show once the prices of your trades are in.' : live ? 'No trades yet.' : 'No paper trades yet.'}
          {pastResult && (
            <>
              {' '}
              In the past, {pastResult.years}, the template did <span className={styles.mono}>{signedPct(pastResult.template)}</span> vs{' '}
              <span className={styles.mono}>{signedPct(pastResult.index)}</span> for an index fund. Its worst year was{' '}
              <span className={styles.mono}>{signedPct(pastResult.worstYear)}</span>.
              {s.templateKey === 'bear' && ' Big drops are rare: it may not signal for years.'}
            </>
          )}
        </p>
      )}
      <dl className={styles.facts}>
        <div>
          <dt>Budget used</dt>
          <dd>
            {money(budget.used, s.currency)} of {money(budget.amount, s.currency)}
            {budget.totalSteps !== null && ` · ${budget.steps} of ${budget.totalSteps} steps`}
          </dd>
        </div>
        {signals && (
          <div>
            <dt>Signals</dt>
            <dd>
              {signals.did} did · {signals.skipped} skipped · {signals.missed} missed
            </dd>
          </div>
        )}
      </dl>
    </div>
  )
}

function Stocks({ s }: { s: StrategyDetail }) {
  return (
    <Section
      title={`Stocks · ${s.stocks.length}`}
      // The stock list editor isn't built yet.
      aside={<TextAction>Edit list</TextAction>}
    >
      <ul className={styles.stocks}>
        {s.stocks.map((row) => {
          const line = stockLine(row, s.mode, s.currency, s.words)
          const content = (
            <>
              <div className={styles.stockMain}>
                <div className={styles.stockName}>{row.name}</div>
                <div className={styles.stockStatus}>
                  <span className={styles.pill} data-tone={line.tone}>
                    {line.pill}
                  </span>
                  <span className={styles.detail}>{line.detail}</span>
                </div>
              </div>
              <div className={styles.stockResult}>
                <div className={styles.result} data-tone={pctTone(row.result)}>
                  {line.result}
                </div>
                <div className={styles.detail}>{line.note}</div>
              </div>
            </>
          )
          // Only a waiting signal leads somewhere yet: the stock page isn't built.
          return (
            <li key={row.assetId}>
              {row.status === 'waitingForYou' && row.signalId ? (
                <Link to={signalPath(s.id, row.signalId)} className={styles.stock}>
                  {content}
                </Link>
              ) : (
                <div className={styles.stock}>{content}</div>
              )}
            </li>
          )
        })}
      </ul>
      <p className={styles.caption}>Removing a stock stops its signals. Your trades in it stay in the results.</p>
    </Section>
  )
}

/** A tile for a screen that isn't built yet: shown, but not usable. */
function SoonTile({ title, sub }: { title: string; sub: string }) {
  return (
    <div className={styles.tile} aria-disabled="true" title="Not available yet">
      <span className={`ui-strong ${styles.tileTitle}`}>{title}</span>
      <span className={styles.detail}>{sub}</span>
    </div>
  )
}

/** Strategy page (wireframe `StrategyDetail`), for both modes. */
export function StrategyPage() {
  const { id = '' } = useParams()
  const strategy = useStrategy(id)
  // Renaming isn't built yet.
  const top = <TopBar back={dashboardPath} aside={<TextAction>Rename</TextAction>} />

  return (
    <LoadableScreen state={strategy} top={top} what="this strategy">
      {(s) => {
        const live = s.mode === 'live'
        const since = s.startedOn === s.today ? 'started today' : `since ${shortDate(s.startedOn)}`
        const source = live
          ? 'Your results come from the trades you confirmed, at your prices.'
          : "Results come from paper trades at the next day's opening price."
        return (
          <Screen top={top} note={`${source} ${DISCLAIMER} ${PAST_RESULTS_CAPTION}`}>
            <div className={styles.header}>
              <h1 className={styles.title}>{s.name}</h1>
              <div className={styles.sub}>
                <Tag tone={live ? 'live' : 'sim'}>{live ? 'LIVE' : 'SIM'}</Tag>
                <span>
                  <em>{s.templateName}</em> · {since}
                </span>
              </div>
            </div>
            <Summary s={s} />
            <Stocks s={s} />
            <div className={styles.tiles}>
              <SoonTile title="Tune the settings" sub="Change its rules" />
              <SoonTile title="Backtest" sub="How the template did" />
            </div>
          </Screen>
        )
      }}
    </LoadableScreen>
  )
}
