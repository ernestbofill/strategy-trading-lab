import { Link, useParams } from 'react-router-dom'
import type { StrategyDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { money, pctTone, shortDate, signedPct, signedPctOrDash } from '../../lib/format'
import { signalPath, strategySettingsPath } from '../dashboard/paths'
import { dashboardPath } from '../new-strategy/paths'
import { ResultsChart } from './ResultsChart'
import styles from './StrategyPage.module.css'
import { SignalsAndTrades } from './SignalsAndTrades'
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
  const t = useT()
  const live = s.mode === 'live'
  const { results, pastResult, budget, signals } = s
  return (
    <div className={styles.summary}>
      {results ? (
        <>
          <div className={styles.figures} data-count={live ? 3 : 2}>
            <Figure label={live ? t('strategy.figure.you') : t('strategy.figure.sinceStart')} value={results.you} highlight />
            {live && <Figure label={t('strategy.figure.rules')} value={results.rules} />}
            <Figure label={t('strategy.figure.index')} value={results.index} />
          </div>
          <ResultsChart history={results.history} live={live} />
          {pastResult && (
            // The strategy has no worst year of its own until it has run a year: the template's stands in.
            <p className={styles.worst}>
              {t('strategy.worstYear', { years: pastResult.years, pct: signedPct(pastResult.worstYear) })}
            </p>
          )}
        </>
      ) : (
        <p className={styles.text}>
          {budget.used > 0
            ? t('strategy.noResults.budget')
            : live
            ? t('strategy.noResults.live')
            : t('strategy.noResults.sim')}
          {pastResult && (
            <>
              {' '}
              {t('strategy.pastSummary.before', { years: pastResult.years })}{' '}
              <span className={styles.mono}>{signedPct(pastResult.template)}</span> {t('strategy.pastSummary.vs')}{' '}
              <span className={styles.mono}>{signedPct(pastResult.index)}</span> {t('strategy.pastSummary.after')}{' '}
              <span className={styles.mono}>{signedPct(pastResult.worstYear)}</span>.
              {s.templateKey === 'bear' && ' Big drops are rare: it may not signal for years.'}
            </>
          )}
        </p>
      )}
      <dl className={styles.facts}>
        <div>
          <dt>{t('strategy.budgetUsed')}</dt>
          <dd>
            {t('strategy.budgetRatio', { used: money(budget.used, s.currency), amount: money(budget.amount, s.currency) })}
            {budget.totalSteps !== null && ` · ${t('strategy.budgetSteps', { steps: budget.steps ?? 0, total: budget.totalSteps })}`}
          </dd>
        </div>
        {signals && (
          <div>
            <dt>{t('strategy.signals')}</dt>
            <dd>
              {t('strategy.signalsCount', { did: signals.did, skipped: signals.skipped, missed: signals.missed })}
            </dd>
          </div>
        )}
      </dl>
    </div>
  )
}

function Stocks({ s }: { s: StrategyDetail }) {
  const t = useT()
  return (
    <Section
      title={t('strategy.stocks', { count: s.stocks.length })}
      // The stock list editor isn't built yet.
      aside={<TextAction>{t('strategy.editList')}</TextAction>}
    >
      <ul className={styles.stocks}>
        {s.stocks.map((row) => {
          const line = stockLine(row, s.mode, s.currency, s.words, t)
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
      <p className={styles.caption}>{t('strategy.caption')}</p>
    </Section>
  )
}

/** A tile for a screen that isn't built yet: shown, but not usable. */
function SoonTile({ title, sub }: { title: string; sub: string }) {
  const t = useT()
  return (
    <div className={styles.tile} aria-disabled="true" title={t('strategy.notAvailable')}>
      <span className={`ui-strong ${styles.tileTitle}`}>{title}</span>
      <span className={styles.detail}>{sub}</span>
    </div>
  )
}

/** Strategy page (wireframe `StrategyDetail`), for both modes. */
export function StrategyPage() {
  const t = useT()
  const { id = '' } = useParams()
  const strategy = useStrategy(id)
  // Renaming isn't built yet.
  const top = <TopBar back={dashboardPath} aside={<TextAction>{t('strategy.rename')}</TextAction>} />

  return (
    <LoadableScreen state={strategy} top={top} what={t('strategy.what')}>
      {(s) => {
        const live = s.mode === 'live'
        const since = s.startedOn === s.today ? t('strategy.startedToday') : t('strategy.since', { date: shortDate(s.startedOn) })
        const source = live ? t('strategy.source.live') : t('strategy.source.sim')
        return (
          <Screen top={top} note={`${source} ${t('common.disclaimer')} ${t('common.pastResults')}`}>
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
            <SignalsAndTrades s={s} />
            <div className={styles.tiles}>
              <Link to={strategySettingsPath(s.id)} className={styles.tileLink}>
                <span className={`ui-strong ${styles.tileTitle}`}>{t('strategy.tile.tuneTitle')}</span>
                <span className={styles.detail}>{t('strategy.tile.tuneSub')}</span>
              </Link>
              <SoonTile title={t('strategy.tile.backtestTitle')} sub={t('strategy.tile.backtestSub')} />
            </div>
          </Screen>
        )
      }}
    </LoadableScreen>
  )
}
