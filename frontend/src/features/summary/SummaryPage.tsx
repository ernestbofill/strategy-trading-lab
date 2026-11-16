import { Link } from 'react-router-dom'
import { LinkList, LinkSection } from '../../components/LinkList'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Tag } from '../../components/Tag'
import { ModeTag } from '../../components/ModeTag'
import { useT } from '../../i18n/I18n'
import { fullDate, shortDate } from '../../lib/format'
import { useCurrentAccount } from '../account/AccountContext'
import { aiRoundPath, signalPath, stockPath, strategyPath } from '../dashboard/paths'
import { pickPath } from '../new-strategy/paths'
import styles from './SummaryPage.module.css'
import { useSummary } from './useSummary'

export function SummaryPage() {
  const t = useT()
  const { locale } = useCurrentAccount()
  const summary = useSummary()
  const top = <TopBar home={true} />

  return (
    <LoadableScreen state={summary} top={top} what={t('summary.what')}>
      {(data) => {
        const hasWaiting = data.waitingSignals.length > 0 || data.waitingAiRounds.length > 0
        return (
          <Screen top={top}>
            <div className={styles.page}>
              <PageHeader
                title={t('summary.heading', { date: fullDate(data.date, locale) })}
                lead={t('summary.lead')}
              />

              {!data.hasChanges && (
                <div className={styles.quietCard}>
                  <h2 className={`ui-strong ${styles.quietTitle}`}>{t('summary.quiet.title')}</h2>
                  <p className={styles.quietBody}>{t('summary.quiet.body')}</p>
                </div>
              )}

              {data.hasChanges && hasWaiting && (
                <LinkSection title={t('summary.waiting.title')} count={data.waitingSignals.length + data.waitingAiRounds.length}>
                  <LinkList
                    items={[
                      ...data.waitingSignals.map((s) => ({
                        key: s.signalId,
                        to: signalPath(s.strategyId, s.signalId),
                        tag: <ModeTag live />,
                        children: (
                          <>
                            <span className={`ui-strong ${styles.name}`}>
                              {t('summary.waiting.signal', { stock: s.stockName, strategy: s.strategyName })}
                            </span>
                            {s.stepNo && s.maxSteps && (
                              <span className={styles.rowSub}>{t('summary.waiting.step', { step: s.stepNo, max: s.maxSteps })}</span>
                            )}
                          </>
                        ),
                      })),
                      ...data.waitingAiRounds.map((r) => ({
                        key: r.strategyId,
                        to: aiRoundPath(r.strategyId),
                        tag: <Tag tone="ai">AI</Tag>,
                        children: <span className={`ui-strong ${styles.name}`}>{t('summary.waiting.aiRound', { strategy: r.strategyName })}</span>,
                      })),
                    ]}
                  />
                </LinkSection>
              )}

              {data.hasChanges && data.whatChanged.length > 0 && (
                <LinkSection title={t('summary.whatChanged.title')} count={data.whatChanged.length}>
                  <LinkList
                    items={data.whatChanged.map((c) => ({
                      key: c.signalId,
                      to: signalPath(c.strategyId, c.signalId),
                      tag: <ModeTag live={c.mode === 'live'} />,
                      children: <span className={styles.name}>{c.text}</span>,
                    }))}
                  />
                </LinkSection>
              )}

              {data.hasChanges && data.stockChanges.length > 0 && (
                <LinkSection title={t('summary.stockChanges.title')} count={data.stockChanges.length}>
                  <LinkList
                    items={data.stockChanges.map((c) => ({
                      key: `${c.strategyId}-${c.assetId}-${c.kind}`,
                      to: stockPath(c.strategyId, c.assetId),
                      tag: <ModeTag live={c.mode === 'live'} />,
                      children: <span className={styles.name}>{c.text}</span>,
                    }))}
                  />
                </LinkSection>
              )}

              <LinkSection title={t('summary.strategies.title')}>
                {data.strategies.length > 0 ? (
                  <LinkList
                    items={data.strategies.map((s) => ({
                      key: s.strategyId,
                      to: strategyPath(s.strategyId),
                      tag: <ModeTag live={s.mode === 'live'} />,
                      children: (
                        <>
                          <span className={`ui-strong ${styles.name}`}>{s.strategyName}</span>
                          <span className={styles.scoreMeta}>
                            <span>{t('summary.strategies.since', { date: shortDate(s.startedOn, locale) })}</span>
                            <span>·</span>
                            <span>{t('summary.strategies.signals', { n: s.signalsCount })}</span>
                          </span>
                        </>
                      ),
                    }))}
                  />
                ) : (
                  <div className={styles.emptyState}>
                    <p className={styles.emptyText}>{t('summary.noStrategies')}</p>
                    <Link to={pickPath} className={styles.emptyLink}>
                      {t('summary.startStrategy')}
                    </Link>
                  </div>
                )}
              </LinkSection>
            </div>
          </Screen>
        )
      }}
    </LoadableScreen>
  )
}
