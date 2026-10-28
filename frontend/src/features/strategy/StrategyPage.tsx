import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import type { AiRoundsSummary, StrategyDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { useI18n, useT } from '../../i18n/I18n'
import { money, pctTone, shortDate, signedPct, signedPctOrDash } from '../../lib/format'
import { aiRoundPath, aiRoundsPath, signalPath, strategySettingsPath } from '../dashboard/paths'
import { dashboardPath } from '../new-strategy/paths'
import { DeleteStrategy } from './DeleteStrategy'
import { AlertsSwitch, ArchiveStrategy, EndStrategy, RenameForm } from './StrategyActions'
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
            : s.aiRounds?.done === 0
              ? t('strategy.noResults.ai')
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
        {s.aiRounds && <AiRoundFacts s={s} rounds={s.aiRounds} />}
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

/** AI strategies: when the next round is due, and how the picks did against the rest. */
function AiRoundFacts({ s, rounds }: { s: StrategyDetail; rounds: AiRoundsSummary }) {
  const t = useT()
  const { locale } = useI18n()
  return (
    <>
      <div>
        <dt>{t('strategy.ai.round')}</dt>
        <dd>
          {rounds.due ? (
            <Link to={aiRoundPath(s.id)}>{t('strategy.ai.dueNow')}</Link>
          ) : rounds.nextDueOn ? (
            t('strategy.ai.nextOn', { date: shortDate(rounds.nextDueOn, locale) })
          ) : (
            '–'
          )}
          {` · ${t('strategy.ai.done', { n: rounds.done })}`}
          {rounds.skipped > 0 && ` · ${t('strategy.ai.skipped', { n: rounds.skipped })}`}
        </dd>
      </div>
      {rounds.picksVsRest && (
        <div>
          <dt>{t('strategy.ai.picksVsRest')}</dt>
          <dd>
            <span className={styles.mono}>{signedPct(rounds.picksVsRest.picks, 1)}</span> {t('strategy.ai.vs')}{' '}
            <span className={styles.mono}>{signedPct(rounds.picksVsRest.rest, 1)}</span>
          </dd>
        </div>
      )}
    </>
  )
}

function Stocks({ s }: { s: StrategyDetail }) {
  const t = useT()
  return (
    <Section title={t('strategy.stocks', { count: s.stocks.length })}>
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

/** Strategy page (wireframe `StrategyDetail`), for both modes. */
export function StrategyPage() {
  const t = useT()
  const { id = '' } = useParams()
  const strategy = useStrategy(id)
  const top = <TopBar back={dashboardPath} />

  return (
    <LoadableScreen state={strategy} top={top} what={t('strategy.what')}>
      {(s) => <StrategyView s={s} />}
    </LoadableScreen>
  )
}

/**
 * A loaded strategy. A running one has "RENAME" in the top bar, the alerts switch (strategies that email) and "End the
 * strategy"; an ended one is read-only, with "Ended on [date]", "Archive" or "Unarchive", and "Delete the strategy".
 */
function StrategyView({ s }: { s: StrategyDetail }) {
  const t = useT()
  const [renaming, setRenaming] = useState(false)
  const live = s.mode === 'live'
  const running = s.status === 'running'
  const since = s.startedOn === s.today ? t('strategy.startedToday') : t('strategy.since', { date: shortDate(s.startedOn) })
  const source = live ? t('strategy.source.live') : t('strategy.source.sim')
  const top = (
    <TopBar
      back={dashboardPath}
      aside={renaming ? undefined : <TextAction onClick={() => setRenaming(true)}>{t('strategy.rename')}</TextAction>}
    />
  )
  return (
    <Screen top={top} note={`${source} ${t('common.disclaimer')} ${t('common.pastResults')}`}>
      <div className={styles.header}>
        {renaming ? <RenameForm s={s} onClose={() => setRenaming(false)} /> : <h1 className={styles.title}>{s.name}</h1>}
        <div className={styles.sub}>
          <Tag tone={live ? 'live' : 'sim'}>{live ? 'LIVE' : 'SIM'}</Tag>
          {s.endedOn && <span className={styles.ended}>{t('strategy.endedOn', { date: shortDate(s.endedOn) })}</span>}
          <span>
            <em>{s.templateName}</em> · {since}
          </span>
        </div>
      </div>
      <Summary s={s} />
      <Stocks s={s} />
      <SignalsAndTrades s={s} />
      {(running || s.aiRounds) && (
        <div className={styles.tiles}>
          {running && (
            <Link to={strategySettingsPath(s.id)} className={styles.tileLink}>
              <span className={`ui-strong ${styles.tileTitle}`}>{t('strategy.tile.tuneTitle')}</span>
              <span className={styles.detail}>{t('strategy.tile.tuneSub')}</span>
            </Link>
          )}
          {s.aiRounds && (
            <Link to={aiRoundsPath(s.id)} className={styles.tileLink}>
              <span className={`ui-strong ${styles.tileTitle}`}>{t('strategy.tile.aiRoundsTitle')}</span>
              <span className={styles.detail}>{t('strategy.tile.aiRoundsSub')}</span>
            </Link>
          )}
        </div>
      )}
      {/* Only strategies that email have alerts: live ones (signals) and AI ones (rounds due). */}
      {running && (live || s.aiRounds) && <AlertsSwitch s={s} />}
      {running ? <EndStrategy s={s} /> : <ArchiveStrategy s={s} />}
      <DeleteStrategy id={s.id} name={s.name} />
    </Screen>
  )
}
