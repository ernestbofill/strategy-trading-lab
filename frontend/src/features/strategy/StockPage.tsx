import type { ReactNode } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import type { CheckGroup, CheckTexts, RuleCheck, StockRow, StrategyDetail } from '../../api'
import { InfoTip } from '../../components/InfoTip'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Stat, Stats } from '../../components/Stats'
import { ModeTag } from '../../components/ModeTag'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { exact, formatRate, money, pctTone, shortDate, signedPct } from '../../lib/format'
import { signalPath, strategyPath } from '../dashboard/paths'
import { checkText, fillCheck, isPastMax } from './checks'
import { PriceChart } from './PriceChart'
import { isCandidateOn } from './priceChartMarks'
import { SignalsAndTrades } from './SignalsAndTrades'
import { StockPill } from './StockPill'
import { notCheckedLine, stockLine, type StockLine } from './stockLine'
import styles from './StockPage.module.css'
import { useStrategy } from './useStrategy'

const WINDOW_KEYS = new Set(['zone', 'buyLine', 'sma200', 'golden', 'near_high', 'breakout'])

/** Visual gauge for a rule check: threshold bar (with >100% overflow), 3-zone window band, or moving-average comparison. */
function CheckGauge({ check, currency }: { check: RuleCheck; currency: string }) {
  const t = useT()
  if (check.met === null && !check.used) return null
  const past = isPastMax(check)
  const state = check.used ? 'used' : past ? 'past' : check.met ? 'met' : 'notMet'

  // 1. Moving-average stack: Short vs Mid vs Long prices.
  if (check.key === 'stacked' && check.level !== null && check.midLevel != null && check.endLevel != null) {
    const firstOk = check.level > check.midLevel
    const secondOk = check.midLevel > check.endLevel
    return (
      <div className={styles.stackStrip} aria-hidden="true">
        <span className={styles.stackChip}>{t('stock.bar.short', { price: exact(check.level, currency) })}</span>
        <span className={styles.stackRel} data-ok={firstOk}>{firstOk ? '>' : '≤'}</span>
        <span className={styles.stackChip}>{t('stock.bar.mid', { price: exact(check.midLevel, currency) })}</span>
        <span className={styles.stackRel} data-ok={secondOk}>{secondOk ? '>' : '≤'}</span>
        <span className={styles.stackChip}>{t('stock.bar.long', { price: exact(check.endLevel, currency) })}</span>
      </div>
    )
  }

  // 2. Rising long average: 20 trading days ago -> today.
  if (check.key === 'rising' && check.level !== null && check.endLevel != null) {
    return (
      <div className={styles.stackStrip} aria-hidden="true">
        <span className={styles.stackChip}>{t('stock.bar.ago20', { price: exact(check.endLevel, currency) })}</span>
        <span className={styles.stackRel} data-ok={check.met === true}>{check.met ? '↗' : '↘'}</span>
        <span className={styles.stackChip}>{t('stock.bar.now', { price: exact(check.level, currency) })}</span>
      </div>
    )
  }

  // 3. Index trend sell line 3-level band (Sell line | SMA 200 | Buy line).
  if (check.key === 'sellLine' && check.max === null && check.level !== null && check.midLevel != null && check.endLevel != null) {
    return (
      <div className={styles.stackStrip} aria-hidden="true">
        <span className={styles.stackChip}>{t('stock.bar.sell', { price: exact(check.level, currency) })}</span>
        <span className={styles.stackRel} data-ok={true}>·</span>
        <span className={styles.stackChip}>{t('stock.bar.sma', { price: exact(check.midLevel, currency) })}</span>
        <span className={styles.stackRel} data-ok={true}>·</span>
        <span className={styles.stackChip}>{t('stock.bar.buy', { price: exact(check.endLevel, currency) })}</span>
      </div>
    )
  }

  // 4. The candidate window (`beaten`) has no gauge: a bar of days read as "more is better".
  if (check.key === 'beaten') return null

  // 5. Band / window checks where going past `max` cancels the signal (`zone`, `buyLine`, milestones).
  if (WINDOW_KEYS.has(check.key) && check.value !== null && check.max !== null && check.max > 0) {
    const isZone = check.key === 'zone'
    const min = isZone ? (check.min ?? 0) : 0
    const max = check.max
    const span = Math.max(1, max - min)
    let fillPct: number
    if (check.key === 'golden' && check.met === false && !past) {
      fillPct = 18
    } else if (check.value < min) {
      fillPct = isZone
        ? Math.max(4, Math.round((Math.max(0, check.value) / Math.max(1, min)) * 30))
        : Math.max(4, Math.round(30 + (Math.max(-15, check.value) / 15) * 26))
    } else if (check.value <= max) {
      fillPct = Math.round(30 + ((check.value - min) / span) * 45)
    } else {
      fillPct = Math.min(100, Math.round(75 + ((check.value - max) / max) * 25))
    }
    const label = isZone
      ? t('stock.bar.zone', { min: formatRate(min), max: formatRate(max) })
      : t('stock.bar.chase', { max: formatRate(max) })
    return (
      <div className={styles.progress}>
        <div className={styles.bar} data-kind="band" aria-hidden="true">
          <span className={styles.bandZone} />
          <div className={styles.fill} data-state={state} style={{ width: `${fillPct}%` }} />
          <span className={styles.targetTick} style={{ left: '30%' }} />
          <span className={styles.targetTick} style={{ left: '75%' }} />
        </div>
        <span className={styles.mono} data-state={state}>{label}</span>
      </div>
    )
  }

  // 6. Threshold progress bar (supports > 100% overflow past the 75% target marker).
  if (check.progress !== null) {
    const pct = Math.round(check.progress * 100)
    const hasOverflow = pct > 100
    const fillPct = hasOverflow
      ? Math.min(100, Math.round(75 + Math.min(25, ((pct - 100) / 100) * 25)))
      : Math.round((pct / 100) * 75)
    return (
      <div className={styles.progress}>
        <div className={styles.bar} data-kind="threshold" aria-hidden="true">
          <div className={styles.fill} data-state={state} style={{ width: `${fillPct}%` }} />
          <span className={styles.targetTick} style={{ left: '75%' }} />
        </div>
        <span className={styles.mono} data-state={state}>{pct}%</span>
      </div>
    )
  }

  // 7. Price vs single average line (e.g. `floor` in Winners at a discount, `vsSma200` in The comeback): 0% line at 50%.
  if (check.level !== null && check.value !== null) {
    const fillPct = Math.max(4, Math.min(100, Math.round(50 + (Math.max(-25, Math.min(25, check.value)) / 25) * 50)))
    return (
      <div className={styles.progress}>
        <div className={styles.bar} data-kind="threshold" aria-hidden="true">
          <div className={styles.fill} data-state={state} style={{ width: `${fillPct}%` }} />
          <span className={styles.targetTick} style={{ left: '50%' }} />
        </div>
        <span className={styles.mono} data-state={state}>{signedPct(check.value)}</span>
      </div>
    )
  }

  return null
}

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
  return (
    <li className={styles.check}>
      <div className={styles.checkMain}>
        <div className={styles.checkLabel}>
          {texts.label}
          {texts.info && <InfoTip label={t('stock.check.about', { label: texts.label })} text={fillCheck(texts.info, check, currency)} />}
        </div>
        <div className={styles.checkDetail}>{checkText(check, texts, currency, 'detail')}</div>
      </div>
      {mark && (
        <span className={styles.mark} data-met={state} title={mark}>
          <span aria-hidden="true">{icon}</span>
          <span className={styles.markText}>{mark}</span>
        </span>
      )}
      <CheckGauge check={check} currency={currency} />
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
          currency={row.currency}
          trades={trades}
          texts={s.checkTexts}
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
              <span className={styles.muted}>{t('stock.close', { date: shortDate(row.checkedOn) })}</span>
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
      <SignalsAndTrades s={own} />
    </Screen>
  )
}
