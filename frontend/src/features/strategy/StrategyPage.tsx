import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import type { AiRoundsSummary, StrategyDetail } from '../../api'
import { InfoTip } from '../../components/InfoTip'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Stat, Stats } from '../../components/Stats'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { useI18n, useT } from '../../i18n/I18n'
import { exact, money, pctTone, shortDate, signedPct, signedPctOrDash } from '../../lib/format'
import { aiRoundPath, aiRoundsPath, signalPath, stockPath, strategyListPath, strategyPath, strategySettingsPath } from '../dashboard/paths'
import { DeleteStrategy } from './DeleteStrategy'
import { ArchiveStrategy, GoLive, RenameForm } from './StrategyActions'
import { ResultsChart } from './ResultsChart'
import styles from './StrategyPage.module.css'
import { SignalsAndTrades } from './SignalsAndTrades'
import { StockRules, StockStatus } from './StockPage'
import { StockPill } from './StockPill'
import { followedStocks, stockLine } from './stockLine'
import { useStrategy } from './useStrategy'

function Figure({ label, value, highlight, tip }: { label: string; value: number | null; highlight?: boolean; tip?: ReactNode }) {
  return (
    <div className={styles.figure}>
      <div className={styles.figureLabel}>
        {label}
        {tip}
      </div>
      <div className={styles.figureValue} data-tone={highlight ? pctTone(value) : undefined}>
        {signedPctOrDash(value, 1)}
      </div>
    </div>
  )
}

/**
 * The summary: tiles first (a fund's status, budget used and, live, how the signals were answered), then the results
 * box, or what was tested before the first buy.
 */
function Summary({ s, status }: { s: StrategyDetail; status?: ReactNode }) {
  const t = useT()
  const live = s.mode === 'live'
  const { results, pastResult, budget, signals } = s
  return (
    <div className={styles.summary}>
      <Stats>
        {status}
        <Stat label={t('strategy.budgetUsed')}>
          {t('strategy.budgetRatio', { used: money(budget.used, s.currency), amount: money(budget.amount, s.currency) })}
          {budget.totalSteps !== null && ` · ${t('strategy.budgetSteps', { steps: budget.steps ?? 0, total: budget.totalSteps })}`}
        </Stat>
        {s.aiRounds && <AiRoundFacts s={s} rounds={s.aiRounds} />}
        {signals && (
          <Stat label={t('strategy.signals')}>{t('strategy.signalsCount', { did: signals.did, skipped: signals.skipped, missed: signals.missed })}</Stat>
        )}
      </Stats>
      {results ? (
        <div className={styles.results}>
          <div className={styles.figures} data-count={live ? 3 : 2}>
            <Figure label={live ? t('strategy.figure.you') : t('strategy.figure.sinceStart')} value={results.you} highlight />
            {live && <Figure label={t('strategy.figure.rules')} value={results.rules} />}
            <Figure
              label={t('strategy.figure.index')}
              value={results.index}
              tip={<InfoTip label={t('common.indexTip.label')} text={t('common.indexTip.text')} />}
            />
          </div>
          <ResultsChart history={results.history} live={live} />
          {pastResult && (
            // The strategy has no worst year of its own until it has run a year: the template's stands in.
            <p className={styles.worst}>
              {t('strategy.worstYear', {
                years: pastResult.years,
                pct: signedPct(pastResult.worstYear),
                index: signedPct(pastResult.indexWorstYear),
              })}
            </p>
          )}
        </div>
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
              {pastResult.isCustomized
                ? t('strategy.pastSummary.custom')
                : t('strategy.pastSummary.before', { years: pastResult.years })}{' '}
              <span className={styles.mono}>{signedPct(pastResult.templatePerYear, 1)}</span> {t('strategy.pastSummary.vs')}{' '}
              <span className={styles.mono}>{signedPct(pastResult.indexPerYear, 1)}</span> {t('strategy.pastSummary.after')}{' '}
              <span className={styles.mono}>{signedPct(pastResult.worstYear)}</span>{t('strategy.pastSummary.indexWorst')}{' '}
              <span className={styles.mono}>{signedPct(pastResult.indexWorstYear)}</span>.
              {s.templateKey === 'bear' && ` ${t('dashboard.card.bearRare')}`}
            </>
          )}
        </p>
      )}
    </div>
  )
}

/** AI strategies: when the next round is due, and how the picks did against the rest. */
function AiRoundFacts({ s, rounds }: { s: StrategyDetail; rounds: AiRoundsSummary }) {
  const t = useT()
  const { locale } = useI18n()
  return (
    <>
      <Stat label={t('strategy.ai.round')}>
        {rounds.due ? (
          <Link to={aiRoundPath(s.id)}>{t('strategy.ai.dueNow')}</Link>
        ) : rounds.nextDueOn ? (
          t('strategy.ai.nextOn', { date: shortDate(rounds.nextDueOn, locale) })
        ) : (
          '–'
        )}
        {` · ${t('strategy.ai.done', { n: rounds.done })}`}
        {rounds.skipped > 0 && ` · ${t('strategy.ai.skipped', { n: rounds.skipped })}`}
      </Stat>
      {rounds.picksVsRest && (
        <Stat label={t('strategy.ai.picksVsRest')}>
          <span className={styles.mono}>{signedPct(rounds.picksVsRest.picks, 1)}</span> {t('strategy.ai.vs')}{' '}
          <span className={styles.mono}>{signedPct(rounds.picksVsRest.rest, 1)}</span>
        </Stat>
      )}
    </>
  )
}

function Stocks({ s }: { s: StrategyDetail }) {
  const t = useT()
  return (
    <Section
      title={t('strategy.stocks', { count: followedStocks(s).length })}
      aside={s.status === 'running' ? <TextAction to={strategyListPath(s.id)}>{t('strategy.editList')}</TextAction> : undefined}
    >
      <ul className={styles.stocks}>
        {s.stocks.map((row) => {
          const line = stockLine(row, s.mode, s.currency, s.words, s.checkTexts, t)
          const content = (
            <>
              <div className={styles.stockMain}>
                <div className={styles.stockName}>{row.name}</div>
                <div className={styles.stockStatus}>
                  <StockPill tone={line.tone}>{line.pill}</StockPill>
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
          // Every row opens its stock page; a signal waiting for the user is answered from there.
          return (
            <li key={row.assetId}>
              <Link to={stockPath(s.id, row.assetId)} className={styles.stock} data-removed={row.status === 'removed' || undefined}>
                {content}
              </Link>
            </li>
          )
        })}
      </ul>
    </Section>
  )
}

/** Strategy page (wireframe `StrategyDetail`), for both modes. */
export function StrategyPage() {
  const t = useT()
  const { id = '' } = useParams()
  const strategy = useStrategy(id)
  const top = <TopBar home aside={null} />

  return (
    <LoadableScreen state={strategy} top={top} what={t('strategy.what')}>
      {(s) => <StrategyView s={s} />}
    </LoadableScreen>
  )
}

/**
 * A loaded strategy. A running one has "RENAME" in the top bar and "Archive the strategy"; an archived one is read-only,
 * with "Archived on [date]" and "Delete the strategy". A single-fund strategy inlines its fund's status, price chart and
 * rule checks directly on this page instead of a separate stock page.
 */
function StrategyView({ s }: { s: StrategyDetail }) {
  const t = useT()
  const [renaming, setRenaming] = useState(false)
  const live = s.mode === 'live'
  const running = s.status === 'running'
  const fund = s.investsIn !== 'stockList' ? s.stocks[0] : undefined
  const answer =
    fund?.status === 'waitingForYou' && fund.signalId ? <PrimaryAction to={signalPath(s.id, fund.signalId)}>{t('stock.answer')}</PrimaryAction> : undefined
  const since = s.startedOn === s.today ? t('strategy.startedToday') : t('strategy.since', { date: shortDate(s.startedOn) })
  const source = live ? t('strategy.source.live') : t('strategy.source.sim')
  const top = (
    <TopBar
      home
      aside={renaming ? null : <TextAction onClick={() => setRenaming(true)}>{t('strategy.rename')}</TextAction>}
    />
  )
  return (
    <Screen top={top} action={answer} note={source}>
      <div className={styles.header}>
        {renaming ? (
          <RenameForm s={s} onClose={() => setRenaming(false)} />
        ) : (
          <div className={styles.titleRow}>
            <h1 className={styles.title}>{s.name}</h1>
            {fund?.close != null && fund.checkedOn && (
              <div className={styles.price}>
                <span className={styles.priceValue}>{exact(fund.close, fund.currency)}</span>
                <span className={styles.muted}>{t('stock.close', { date: shortDate(fund.checkedOn) })}</span>
              </div>
            )}
          </div>
        )}
        {fund && (
          <>
            <div className={styles.fundName}>
              <span className={styles.fundSymbol}>{fund.symbol}</span> · {fund.name}
              {fund.exchange && <span className={styles.muted}> · {fund.exchange}</span>}
            </div>
            {fund.what && fund.what !== fund.name && <p className={styles.what}>{fund.what}</p>}
          </>
        )}
        <div className={styles.sub}>
          <Tag tone={live ? 'live' : 'sim'}>{live ? 'LIVE' : 'SIM'}</Tag>
          {s.endedOn && <span className={styles.ended}>{t('strategy.archivedOn', { date: shortDate(s.endedOn) })}</span>}
          <span>
            <em>{s.templateName}</em> · {since}
          </span>
        </div>
        {s.sourceStrategyId && s.sourceStrategyName && (
          <p className={styles.copiedFrom}>
            {t('strategy.copiedFrom')} <Link to={strategyPath(s.sourceStrategyId)}>{s.sourceStrategyName}</Link>
          </p>
        )}
      </div>
      {fund ? (
        <StockRules
          s={s}
          row={fund}
          stats={<Summary s={s} status={<StockStatus line={stockLine(fund, s.mode, s.currency, s.words, s.checkTexts, t)} />} />}
        />
      ) : (
        <>
          <Summary s={s} />
          <Stocks s={s} />
        </>
      )}
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
          {/* A running simulation can start a live copy; the two keep separate histories. */}
          {running && !live && (
            <GoLive s={s} className={styles.tileLink} titleClassName={`ui-strong ${styles.tileTitle}`} detailClassName={styles.detail} />
          )}
        </div>
      )}
      {/* One way out of each state: a running strategy is archived, and only an archived one can be deleted. */}
      {running ? <ArchiveStrategy s={s} /> : <DeleteStrategy id={s.id} name={s.name} />}
    </Screen>
  )
}
