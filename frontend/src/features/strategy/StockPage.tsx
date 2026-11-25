import type { ReactNode } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import type { CheckGroup, CheckTexts, RuleCheck, StockRow, StrategyDetail } from '../../api'
import { InfoTip } from '../../components/InfoTip'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Stat, Stats } from '../../components/Stats'
import { ModeTag } from '../../components/ModeTag'
import { useI18n, useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { exact, money, pctTone, shortDate } from '../../lib/format'
import { signalPath, stockPath, strategyPath } from '../dashboard/paths'
import { useRole } from '../account/AccountContext'
import { backtestPath } from '../backtest/paths'
import { checkText, fillCheck, isPastMax } from './checks'
import { CheckGauge } from './CheckGauge'
import { PriceChart } from './PriceChart'
import { isCandidateOn } from './priceChartMarks'
import { SignalsAndTrades } from './SignalsAndTrades'
import { StockPill } from './StockPill'
import { notCheckedLine, stockLine, stockPriceLabel, type StockLine } from './stockLine'
import styles from './StockPage.module.css'
import { useStrategy } from './useStrategy'

/** One rule: its name (with an "i"), where the stock stands, a verdict badge, and a visual gauge. */
function Check({ check, texts, currency }: { check: RuleCheck; texts: CheckTexts; currency: string }) {
  const t = useT()
  const past = isPastMax(check)
  const pastKey: MessageKey =
    check.key === 'beaten' ? 'stock.check.windowClosed' : check.key === 'zone' ? 'stock.check.pastZone' : 'stock.check.pastLimit'
  const mark = check.used
    ? t('stock.check.used')
    : past
      ? t(pastKey)
      : check.met === null
        ? null
        : t(check.met ? 'stock.check.met' : 'stock.check.notMet')
  const state = check.used ? 'used' : past ? 'past' : String(check.met)
  const icon = check.used || check.met ? '✓' : past ? '✕' : '○'
  // A used rule: the step it bought and when; today's figures no longer matter.
  const detail = !check.used
    ? checkText(check, texts, currency, 'detail')
    : check.date
      ? t('stock.check.boughtOn', { date: shortDate(check.date) })
      : fillCheck(texts.detail, check, currency)
  return (
    <li className={styles.check}>
      <div className={styles.checkMain}>
        <div className={styles.checkLabel}>
          {texts.label}
          {texts.info && <InfoTip label={t('stock.check.about', { label: texts.label })} text={fillCheck(texts.info, check, currency)} />}
        </div>
        <div className={styles.checkDetail}>{detail}</div>
      </div>
      <CheckGauge check={check} currency={currency} className={styles.gauge} />
      {mark && (
        <span className={styles.mark} data-met={state} title={mark}>
          <span aria-hidden="true">{icon}</span>
          <span className={styles.markText}>{mark}</span>
        </span>
      )}
    </li>
  )
}

const GROUPS: { group: CheckGroup; title: MessageKey }[] = [
  { group: 'candidate', title: 'stock.group.candidate' },
  { group: 'buy', title: 'stock.group.buy' },
  { group: 'next', title: 'stock.group.next' },
  { group: 'sell', title: 'stock.group.sell' },
]

function candidateDateOf(row: StockRow): string | null {
  if (row.candidateSince) return row.candidateSince
  // Only for a check stored without it: a met candidate check's date, which for the comeback's "beaten" is the last day it
  // was beaten down, not the start of the run (#275). The dip, comeback and bear store candidateSince on every candidate day.
  return row.checks.find((c) => c.group === 'candidate' && c.met === true && c.date)?.date ?? null
}

/** The rules, in their parts: what makes it a candidate, what gives the signal, what earns the next step. */
function Rules({
  row,
  texts,
  isCandidate,
  since,
}: {
  row: StockRow
  texts: Record<string, CheckTexts>
  isCandidate: boolean
  since: string | null
}) {
  const t = useT()
  if (row.checks.length === 0) return <p className={styles.lead}>{t('stock.rulesLater')}</p>
  const lead = (group: CheckGroup, checks: RuleCheck[]) => {
    switch (group) {
      case 'candidate':
        return since
          ? t('stock.group.candidateYesSince', { date: shortDate(since) })
          : t(isCandidate ? 'stock.group.candidateYes' : 'stock.group.candidateNo')
      case 'buy': {
        if (!isCandidate) return t('stock.group.buyLater')
        const counted = checks.filter((c) => c.met !== null)
        return t('stock.group.buyMet', { met: counted.filter((c) => c.met).length, total: counted.length })
      }
      case 'next':
        return t(checks.every((c) => c.preview) ? 'stock.group.nextPreviewLead' : 'stock.group.nextLead')
      case 'sell':
        return t('stock.group.sellLead')
    }
  }
  return (
    <>
      {GROUPS.map(({ group, title }) => {
        const checks = row.checks.filter((c) => c.group === group && texts[c.key])
        if (checks.length === 0) return null
        const previewNext = group === 'next' && checks.every((c) => c.preview)
        const body = (
          <>
            <h3 className={styles.groupTitle}>{t(title)}</h3>
            <p className={styles.lead}>{lead(group, checks)}</p>
            <ul className={styles.checks}>
              {checks.map((c) => (
                <Check key={c.key} check={c} texts={texts[c.key]} currency={row.currency} />
              ))}
            </ul>
          </>
        )
        if (previewNext) {
          return (
            <details key={group} className={styles.nextPreview}>
              <summary className={styles.nextSummary}>{t('stock.group.nextShow')}</summary>
              <div className={styles.group} data-later>{body}</div>
            </details>
          )
        }
        return (
          <section key={group} className={styles.group} data-later={group === 'buy' && !isCandidate ? true : undefined}>
            {body}
          </section>
        )
      })}
    </>
  )
}

/**
 * A stock's state under the rules as the first tile: the same pill and sentence as its row in the stock list, on one
 * line when they fit (on the stock page, and on a fund strategy's page).
 */
export function StockStatus({ line }: { line: StockLine }) {
  const t = useT()
  return (
    <Stat label={t('stock.status')}>
      <span className={styles.status}>
        <StockPill tone={line.tone}>{line.pill}</StockPill>
        <span>{line.detail}</span>
      </span>
    </Stat>
  )
}

/**
 * A stock's figures, price chart and rule checks (used on the stock page, and directly on a fund strategy's page).
 * `stats` sits above the chart.
 */
export function StockRules({ s, row, stats }: { s: StrategyDetail; row: StockRow; stats?: ReactNode }) {
  const t = useT()
  const isCandidate = isCandidateOn(row.checks)
  const since = isCandidate && row.checks.some((c) => c.group === 'candidate' && c.met !== null) ? candidateDateOf(row) : null
  const trades = s.trades.filter((x) => x.assetId === row.assetId)
  // Without enough price history the rules say nothing yet: the status tile says so.
  const rules = s.checkTexts && row.status !== 'notEnoughData' && (
    <Section title={row.checkedOn ? t('stock.rules', { date: shortDate(row.checkedOn) }) : notCheckedLine(row, t)}>
      <Rules row={row} texts={s.checkTexts} isCandidate={isCandidate} since={since} />
    </Section>
  )
  return (
    <>
      {stats}

      {/* The chart draws the rules' levels as they checked them: none until the first check, which comes right after it's added. */}
      {row.checkedOn && (
        <PriceChart
          strategyId={s.id}
          assetId={row.assetId}
          isCandidate={row.checks.length > 0 ? isCandidate : null}
          candidateSince={row.checks.some((c) => c.key === 'beaten') ? since : null}
          currency={row.currency}
          trades={trades}
        />
      )}

      {rules}
    </>
  )
}

/** The stock page: one stock under one strategy's rules, its position and its signals and trades. */
export function StockPage() {
  const t = useT()
  const { id = '', assetId = '' } = useParams()
  const strategy = useStrategy(id)
  return (
    <LoadableScreen state={strategy} top={<TopBar up={strategyPath(id)} upLabel="common.up.strategy" />} what={t('stock.what')}>
      {(s) => <StockView s={s} assetId={assetId} />}
    </LoadableScreen>
  )
}

function StockView({ s, assetId }: { s: StrategyDetail; assetId: string }) {
  const t = useT()
  const { locale } = useI18n()
  const owner = useRole() === 'owner'
  if (s.investsIn !== 'stockList') return <Navigate to={strategyPath(s.id)} replace />
  const top = <TopBar up={strategyPath(s.id)} upLabel="common.up.strategy" />
  const row = s.stocks.find((r) => r.assetId === assetId)
  if (!row) {
    return (
      <Screen top={top}>
        <ScreenMessage>{t('stock.notInStrategy')}</ScreenMessage>
      </Screen>
    )
  }
  const live = s.mode === 'live'
  const line = stockLine(row, s.mode, s.currency, s.words, s.checkTexts, t)
  const answer =
    row.status === 'waitingForYou' && row.signalId ? <PrimaryAction to={signalPath(s.id, row.signalId)}>{t('stock.answer')}</PrimaryAction> : undefined
  // This stock's own history; the trade form offers only this stock.
  const own: StrategyDetail = {
    ...s,
    stocks: [row],
    trades: s.trades.filter((x) => x.assetId === assetId),
    signalHistory: s.signalHistory.filter((x) => x.assetId === assetId),
  }
  return (
    <Screen top={top} action={answer}>
      <div className={styles.header}>
        <div className={styles.titleRow}>
          <h1 className={styles.symbol}>{row.symbol}</h1>
          {row.close !== null && row.checkedOn && (
            <div className={styles.price}>
              <span className={styles.priceValue}>{exact(row.close, row.currency)}</span>
              <span className={styles.muted}>{stockPriceLabel(row, locale, t)}</span>
            </div>
          )}
        </div>
        <div className={styles.name}>
          {row.name}
          {row.exchange && <span className={styles.muted}> · {row.exchange}</span>}
        </div>
        {row.what && row.what !== row.name && <p className={styles.what}>{row.what}</p>}
        <div className={styles.strategy}>
          <ModeTag live={live} />
          <Link to={strategyPath(s.id)}>{s.name}</Link>
        </div>
      </div>

      <StockRules
        s={s}
        row={row}
        stats={
          // While it holds the stock, the status already says the steps ("2 of 4 steps"), so there's no Steps tile.
          <Stats>
            <StockStatus line={line} />
            {row.result !== null && (
              <>
                <Stat label={t('stock.position.invested')}>{money(row.invested, s.currency)}</Stat>
                <Stat label={t('stock.position.result')} tone={pctTone(row.result)}>
                  {line.result}
                </Stat>
              </>
            )}
          </Stats>
        }
      />
      {owner && s.investsIn === 'stockList' && s.words && (
        <Link className={styles.testStock} to={backtestPath({ strategy: s.id, stock: row.symbol, from: stockPath(s.id, row.assetId) })}>
          <span>{t('backtest.entry.stock')}</span>
          <span className={styles.muted}>{t('backtest.entry.stockSub')}</span>
        </Link>
      )}
      <SignalsAndTrades s={own} />
    </Screen>
  )
}
