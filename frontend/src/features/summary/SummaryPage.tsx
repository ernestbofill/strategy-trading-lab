import { Link } from 'react-router-dom'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Tag } from '../../components/Tag'
import { ModeTag } from '../../components/ModeTag'
import { useT } from '../../i18n/I18n'
import { fullDate, shortDate } from '../../lib/format'
import { useCurrentAccount } from '../account/AccountContext'
import { pickPath } from '../new-strategy/paths'
import styles from './SummaryPage.module.css'
import { useSummary } from './useSummary'

const isLive = (mode: string) => mode?.toLowerCase() === 'live'

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
                <section className={styles.section} aria-labelledby="summary-waiting-heading">
                  <div className={styles.head}>
                    <h2 id="summary-waiting-heading" className={`ui-strong ${styles.sectionTitle}`}>
                      {t('summary.waiting.title')}
                    </h2>
                    <span className={styles.count}>
                      {data.waitingSignals.length + data.waitingAiRounds.length}
                    </span>
                  </div>
                  <ul className={styles.list}>
                    {data.waitingSignals.map((s) => (
                      <li key={s.signalId}>
                        <Link to={`/strategies/${s.strategyId}/signals/${s.signalId}`} className={styles.row}>
                          <ModeTag live />
                          <div className={styles.rowContent}>
                            <div className={styles.rowMain}>
                              <span className={`ui-strong ${styles.name}`}>
                                {t('summary.waiting.signal', { stock: s.stockName, strategy: s.strategyName })}
                              </span>
                            </div>
                            {s.stepNo && s.maxSteps && (
                              <span className={styles.rowSub}>
                                {t('summary.waiting.step', { step: s.stepNo, max: s.maxSteps })}
                              </span>
                            )}
                          </div>
                          <span className={styles.chevron} aria-hidden="true">›</span>
                        </Link>
                      </li>
                    ))}
                    {data.waitingAiRounds.map((r) => (
                      <li key={r.strategyId}>
                        <Link to={`/strategies/${r.strategyId}/ai-round`} className={styles.row}>
                          <Tag tone="ai">AI</Tag>
                          <div className={styles.rowContent}>
                            <div className={styles.rowMain}>
                              <span className={`ui-strong ${styles.name}`}>
                                {t('summary.waiting.aiRound', { strategy: r.strategyName })}
                              </span>
                            </div>
                          </div>
                          <span className={styles.chevron} aria-hidden="true">›</span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {data.hasChanges && data.whatChanged.length > 0 && (
                <section className={styles.section} aria-labelledby="summary-changed-heading">
                  <div className={styles.head}>
                    <h2 id="summary-changed-heading" className={`ui-strong ${styles.sectionTitle}`}>
                      {t('summary.whatChanged.title')}
                    </h2>
                    <span className={styles.count}>{data.whatChanged.length}</span>
                  </div>
                  <ul className={styles.list}>
                    {data.whatChanged.map((c) => (
                      <li key={c.signalId}>
                        <Link to={`/strategies/${c.strategyId}/signals/${c.signalId}`} className={styles.row}>
                          <ModeTag live={isLive(c.mode)} />
                          <div className={styles.rowContent}>
                            <span className={styles.name}>{c.text}</span>
                          </div>
                          <span className={styles.chevron} aria-hidden="true">›</span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {data.hasChanges && data.stockChanges.length > 0 && (
                <section className={styles.section} aria-labelledby="summary-stocks-heading">
                  <div className={styles.head}>
                    <h2 id="summary-stocks-heading" className={`ui-strong ${styles.sectionTitle}`}>
                      {t('summary.stockChanges.title')}
                    </h2>
                    <span className={styles.count}>{data.stockChanges.length}</span>
                  </div>
                  <ul className={styles.list}>
                    {data.stockChanges.map((c) => (
                      <li key={`${c.strategyId}-${c.assetId}-${c.kind}`}>
                        <Link to={`/strategies/${c.strategyId}/stocks/${c.assetId}`} className={styles.row}>
                          <ModeTag live={isLive(c.mode)} />
                          <div className={styles.rowContent}>
                            <span className={styles.name}>{c.text}</span>
                          </div>
                          <span className={styles.chevron} aria-hidden="true">›</span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              <section className={styles.section} aria-labelledby="summary-strategies-heading">
                <div className={styles.head}>
                  <h2 id="summary-strategies-heading" className={`ui-strong ${styles.sectionTitle}`}>
                    {t('summary.strategies.title')}
                  </h2>
                </div>
                {data.strategies.length > 0 ? (
                  <ul className={styles.list}>
                    {data.strategies.map((s) => (
                      <li key={s.strategyId}>
                        <Link to={`/strategies/${s.strategyId}`} className={styles.row}>
                          <ModeTag live={isLive(s.mode)} />
                          <div className={styles.rowContent}>
                            <div className={styles.rowMain}>
                              <span className={`ui-strong ${styles.name}`}>{s.strategyName}</span>
                            </div>
                            <div className={styles.scoreMeta}>
                              <span>{t('summary.strategies.since', { date: shortDate(s.startedOn, locale) })}</span>
                              <span>·</span>
                              <span>{t('summary.strategies.signals', { n: s.signalsCount })}</span>
                            </div>
                          </div>
                          <span className={styles.chevron} aria-hidden="true">›</span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <div className={styles.emptyState}>
                    <p className={styles.emptyText}>{t('summary.noStrategies')}</p>
                    <Link to={pickPath} className={styles.emptyLink}>
                      {t('summary.startStrategy')}
                    </Link>
                  </div>
                )}
              </section>
            </div>
          </Screen>
        )
      }}
    </LoadableScreen>
  )
}
