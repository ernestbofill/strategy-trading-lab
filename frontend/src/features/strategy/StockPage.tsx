import { Link, useParams } from 'react-router-dom'
import type { CheckGroup, CheckTexts, RuleCheck, StockRow, StrategyDetail } from '../../api'
import { InfoTip } from '../../components/InfoTip'
import { KeyValueList } from '../../components/Lists'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Tag } from '../../components/Tag'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { exact, money, pctTone, shortDate } from '../../lib/format'
import { signalPath, strategyPath } from '../dashboard/paths'
import { checkText, fillCheck } from './checks'
import { SignalsAndTrades } from './SignalsAndTrades'
import { StockPill } from './StockPill'
import { stockLine } from './stockLine'
import styles from './StockPage.module.css'
import { useStrategy } from './useStrategy'

/** One rule: its name (with an "i"), where the stock stands, a mark, and a bar for a figure that must still grow. */
function Check({ check, texts, currency }: { check: RuleCheck; texts: CheckTexts; currency: string }) {
  const t = useT()
  const mark = check.used ? t('stock.check.used') : check.met === null ? null : t(check.met ? 'stock.check.met' : 'stock.check.notMet')
  const progress = check.met === false && check.progress !== null ? Math.round(check.progress * 100) : null
  return (
    <li className={styles.check}>
      <div className={styles.checkMain}>
        <div className={styles.checkLabel}>
          {texts.label}
          {texts.info && <InfoTip label={t('stock.check.about', { label: texts.label })} text={fillCheck(texts.info, check, currency)} />}
        </div>
        <div className={styles.checkDetail}>{checkText(check, texts, currency, 'detail')}</div>
        {progress !== null && (
          <div className={styles.progress}>
            <div className={styles.bar} aria-hidden="true">
              <div className={styles.fill} style={{ width: `${progress}%` }} />
            </div>
            <span className={styles.mono}>{progress}%</span>
          </div>
        )}
      </div>
      {mark && (
        <span className={styles.mark} data-met={check.used ? 'used' : String(check.met)} title={mark}>
          <span aria-hidden="true">{check.used || check.met ? '✓' : '○'}</span>
          <span className="visually-hidden">{mark}</span>
        </span>
      )}
    </li>
  )
}

/** The statuses whose line counts the steps held. */
const HOLDS: StockRow['status'][] = ['building', 'holding', 'done']

const GROUPS: { group: CheckGroup; title: MessageKey }[] = [
  { group: 'candidate', title: 'stock.group.candidate' },
  { group: 'buy', title: 'stock.group.buy' },
  { group: 'next', title: 'stock.group.next' },
  { group: 'sell', title: 'stock.group.sell' },
]

/** The rules, in their parts: what makes it a candidate, what gives the signal, what earns the next step. */
function Rules({ row, texts }: { row: StockRow; texts: Record<string, CheckTexts> }) {
  const t = useT()
  if (row.checks.length === 0) return <p className={styles.lead}>{t('stock.rulesLater')}</p>
  const candidate = row.checks.filter((c) => c.group === 'candidate' && c.met !== null)
  const isCandidate = candidate.every((c) => c.met)
  const lead = (group: CheckGroup, checks: RuleCheck[]) => {
    switch (group) {
      case 'candidate':
        return t(isCandidate ? 'stock.group.candidateYes' : 'stock.group.candidateNo')
      case 'buy': {
        if (!isCandidate) return t('stock.group.buyLater')
        const counted = checks.filter((c) => c.met !== null)
        return t('stock.group.buyMet', { met: counted.filter((c) => c.met).length, total: counted.length })
      }
      case 'next':
        return t('stock.group.nextLead')
      case 'sell':
        return t('stock.group.sellLead')
    }
  }
  return (
    <>
      {GROUPS.map(({ group, title }) => {
        const checks = row.checks.filter((c) => c.group === group && texts[c.key])
        if (checks.length === 0) return null
        return (
          <section key={group} className={styles.group} data-later={group === 'buy' && !isCandidate ? true : undefined}>
            <h3 className={styles.groupTitle}>{t(title)}</h3>
            <p className={styles.lead}>{lead(group, checks)}</p>
            <ul className={styles.checks}>
              {checks.map((c) => (
                <Check key={c.key} check={c} texts={texts[c.key]} currency={row.currency} />
              ))}
            </ul>
          </section>
        )
      })}
    </>
  )
}

/** The stock page (not drawn): one stock under one strategy's rules, its position and its signals and trades. */
export function StockPage() {
  const t = useT()
  const { id = '', assetId = '' } = useParams()
  const strategy = useStrategy(id)
  return (
    <LoadableScreen state={strategy} top={<TopBar back={strategyPath(id)} />} what={t('stock.what')}>
      {(s) => <StockView s={s} assetId={assetId} />}
    </LoadableScreen>
  )
}

function StockView({ s, assetId }: { s: StrategyDetail; assetId: string }) {
  const t = useT()
  const top = <TopBar back={strategyPath(s.id)} />
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
    <Screen top={top} action={answer} note={t('common.disclaimer')}>
      <div className={styles.header}>
        <h1 className={styles.symbol}>{row.symbol}</h1>
        <div className={styles.name}>
          {row.name}
          {row.exchange && <span className={styles.muted}> · {row.exchange}</span>}
        </div>
        {row.what && <p className={styles.what}>{row.what}</p>}
        <div className={styles.strategy}>
          <Tag tone={live ? 'live' : 'sim'}>{live ? 'LIVE' : 'SIM'}</Tag>
          <Link to={strategyPath(s.id)}>{s.name}</Link>
        </div>
      </div>

      <div className={styles.status}>
        <div className={styles.statusLine}>
          <StockPill tone={line.tone}>{line.pill}</StockPill>
          <span>{line.detail}</span>
        </div>
        {row.close !== null && row.checkedOn && (
          <div className={styles.muted}>{t('stock.close', { price: exact(row.close, row.currency), date: shortDate(row.checkedOn) })}</div>
        )}
      </div>

      {/* Without enough price history the rules say nothing yet: the status line says so. */}
      {s.checkTexts && row.status !== 'notEnoughData' && (
        <Section title={row.checkedOn ? t('stock.rules', { date: shortDate(row.checkedOn) }) : t('strategy.stock.notChecked')}>
          <Rules row={row} texts={s.checkTexts} />
        </Section>
      )}

      {row.result !== null && (
        <Section title={live ? t('stock.position') : t('stock.position.sim')}>
          <KeyValueList
            rows={[
              ...(row.maxSteps !== null && HOLDS.includes(row.status) ? [[t('stock.position.steps'), line.detail] as [string, string]] : []),
              [t('stock.position.invested'), <span key="invested" className={styles.mono}>{money(row.invested, s.currency)}</span>],
              [
                t('stock.position.result'),
                <span key="result" className={styles.mono} data-tone={pctTone(row.result)}>
                  {line.result}
                </span>,
              ],
            ]}
          />
        </Section>
      )}

      <SignalsAndTrades s={own} />
    </Screen>
  )
}
