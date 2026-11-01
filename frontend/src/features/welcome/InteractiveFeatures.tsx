import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useT } from '../../i18n/I18n'
import { pickPath } from '../new-strategy/paths'
import styles from './InteractiveFeatures.module.css'

type FeatureKey = 'builder' | 'simulation' | 'tracker'

export function InteractiveFeatures() {
  const t = useT()
  const [activeFeature, setActiveFeature] = useState<FeatureKey>('builder')

  // Builder mock state
  const [builderTemplate, setBuilderTemplate] = useState<'discount' | 'comeback' | 'bear'>('discount')

  // Simulation mock state
  const [simTab, setSimTab] = useState<'perf' | 'trades'>('perf')

  // Tracker mock state
  const [trackerState, setTrackerState] = useState<'none' | 'didIt' | 'skipped' | 'snooze'>('none')

  const toggleFeature = (key: FeatureKey) => {
    setActiveFeature((current) => (current === key ? current : key))
  }

  return (
    <section className={styles.container} aria-labelledby="features-heading">
      <div className={styles.cardList} role="tablist" aria-label={t('welcome.features.title')}>
        {/* Pillar 1: Strategy Builder */}
        <button
          type="button"
          role="tab"
          id="tab-builder"
          aria-selected={activeFeature === 'builder'}
          aria-controls="panel-feature-detail"
          className={styles.featureCard}
          data-active={activeFeature === 'builder'}
          onClick={() => toggleFeature('builder')}
        >
          <div className={styles.cardTop}>
            <span className={styles.tag}>{t('welcome.builder.tag')}</span>
            <span className={styles.badge}>{t('welcome.builder.badge')}</span>
          </div>
          <h3 className={styles.cardTitle}>{t('welcome.builder.title')}</h3>
          <p className={styles.tagline}>"{t('welcome.builder.tagline')}"</p>
          <p className={styles.summary}>{t('welcome.builder.summary')}</p>
          <div className={styles.expandToggle}>
            <span>{activeFeature === 'builder' ? t('welcome.collapse') : t('welcome.expand')}</span>
          </div>
        </button>

        {/* Pillar 2: Simulation */}
        <button
          type="button"
          role="tab"
          id="tab-simulation"
          aria-selected={activeFeature === 'simulation'}
          aria-controls="panel-feature-detail"
          className={styles.featureCard}
          data-active={activeFeature === 'simulation'}
          onClick={() => toggleFeature('simulation')}
        >
          <div className={styles.cardTop}>
            <span className={styles.tag}>{t('welcome.sim.tag')}</span>
            <span className={styles.badge}>{t('welcome.sim.badge')}</span>
          </div>
          <h3 className={styles.cardTitle}>{t('welcome.sim.title')}</h3>
          <p className={styles.tagline}>"{t('welcome.sim.tagline')}"</p>
          <p className={styles.summary}>{t('welcome.sim.summary')}</p>
          <div className={styles.expandToggle}>
            <span>{activeFeature === 'simulation' ? t('welcome.collapse') : t('welcome.expand')}</span>
          </div>
        </button>

        {/* Pillar 3: Trading Tracker */}
        <button
          type="button"
          role="tab"
          id="tab-tracker"
          aria-selected={activeFeature === 'tracker'}
          aria-controls="panel-feature-detail"
          className={styles.featureCard}
          data-active={activeFeature === 'tracker'}
          onClick={() => toggleFeature('tracker')}
        >
          <div className={styles.cardTop}>
            <span className={styles.tag}>{t('welcome.tracker.tag')}</span>
            <span className={styles.badge} data-tone="live">
              {t('welcome.tracker.badge')}
            </span>
          </div>
          <h3 className={styles.cardTitle}>{t('welcome.tracker.title')}</h3>
          <p className={styles.tagline}>"{t('welcome.tracker.tagline')}"</p>
          <p className={styles.summary}>{t('welcome.tracker.summary')}</p>
          <div className={styles.expandToggle}>
            <span>{activeFeature === 'tracker' ? t('welcome.collapse') : t('welcome.expand')}</span>
          </div>
        </button>
      </div>

      {/* Expanded Interactive Detail Panel */}
      <div
        id="panel-feature-detail"
        role="tabpanel"
        aria-labelledby={`tab-${activeFeature}`}
        className={styles.detailPanel}
      >
        {activeFeature === 'builder' && (
          <>
            <ul className={styles.pointsList}>
              <li>{t('welcome.builder.point1')}</li>
              <li>{t('welcome.builder.point2')}</li>
              <li>{t('welcome.builder.point3')}</li>
            </ul>

            {/* Interactive Builder Mock Widget */}
            <div className={styles.mockWidget} aria-label="Interactive strategy builder preview">
              <div className={styles.mockHeader}>
                <span className={styles.mockLabel}>{t('welcome.builder.mock.templateLabel')}</span>
                <span className={styles.tag}>STL // TEMPLATE_ENGINE</span>
              </div>

              <div className={styles.templatePills}>
                <button
                  type="button"
                  className={styles.templatePill}
                  data-active={builderTemplate === 'discount'}
                  onClick={() => setBuilderTemplate('discount')}
                >
                  {t('welcome.builder.mock.t1Name')}
                </button>
                <button
                  type="button"
                  className={styles.templatePill}
                  data-active={builderTemplate === 'comeback'}
                  onClick={() => setBuilderTemplate('comeback')}
                >
                  {t('welcome.builder.mock.t2Name')}
                </button>
                <button
                  type="button"
                  className={styles.templatePill}
                  data-active={builderTemplate === 'bear'}
                  onClick={() => setBuilderTemplate('bear')}
                >
                  {t('welcome.builder.mock.t3Name')}
                </button>
              </div>

              <div className={styles.ruleParams}>
                <div className={styles.paramBlock}>
                  <span className={styles.paramLabel}>{t('welcome.builder.mock.ruleLabel')}</span>
                  <span className={styles.paramValue}>
                    {builderTemplate === 'discount' && 'SMA 200 + 5%–15% discount zone'}
                    {builderTemplate === 'comeback' && 'Breakout above 50-day moving average'}
                    {builderTemplate === 'bear' && 'S&P 500 drop \u2265 20% accumulation'}
                  </span>
                </div>
                <div className={styles.paramBlock}>
                  <span className={styles.paramLabel}>{t('welcome.builder.mock.budgetLabel')}</span>
                  <span className={styles.paramValue}>
                    {builderTemplate === 'discount' && '\u20AC100 / step \u00B7 max 3 steps'}
                    {builderTemplate === 'comeback' && '\u20AC150 / step \u00B7 max 2 steps'}
                    {builderTemplate === 'bear' && '\u20AC200 / step \u00B7 max 5 steps'}
                  </span>
                </div>
              </div>

              <div className={styles.stockRow}>
                <span className={styles.paramLabel}>{t('welcome.builder.mock.stocksLabel')}:</span>
                <span className={styles.stockBadge}>AAPL \u2713</span>
                <span className={styles.stockBadge}>MSFT \u2713</span>
                <span className={styles.stockBadge}>NVDA \u2713</span>
                <span className={styles.stockBadge}>AMZN \u2713</span>
              </div>
            </div>

            <Link to={pickPath} className={styles.ctaLink}>
              {t('welcome.builder.cta')}
            </Link>
          </>
        )}

        {activeFeature === 'simulation' && (
          <>
            <ul className={styles.pointsList}>
              <li>{t('welcome.sim.point1')}</li>
              <li>{t('welcome.sim.point2')}</li>
              <li>{t('welcome.sim.point3')}</li>
            </ul>

            {/* Interactive Simulation Mock Widget */}
            <div className={styles.mockWidget} aria-label="Interactive paper trading simulation preview">
              <div className={styles.mockHeader}>
                <span className={styles.badge}>{t('welcome.sim.mock.mode')}</span>
                <span className={styles.tag}>STL // FORWARD_TEST</span>
              </div>

              <div className={styles.simStats}>
                <div className={styles.statBox}>
                  <span className={styles.statLabel}>{t('welcome.sim.mock.returnLabel')}</span>
                  <span className={`${styles.statValue} ${styles.statPositive}`}>+14.2%</span>
                </div>
                <div className={styles.statBox}>
                  <span className={styles.statLabel}>{t('welcome.sim.mock.indexLabel')}</span>
                  <span className={`${styles.statValue} ${styles.statIndex}`}>+8.1%</span>
                </div>
                <div className={styles.statBox}>
                  <span className={styles.statLabel}>{t('welcome.sim.mock.worstLabel')}</span>
                  <span className={`${styles.statValue} ${styles.statNegative}`}>-12.4%</span>
                </div>
              </div>

              <div className={styles.simSubTabs}>
                <button
                  type="button"
                  className={styles.simSubTab}
                  data-active={simTab === 'perf'}
                  onClick={() => setSimTab('perf')}
                >
                  {t('welcome.sim.mock.tabPerf')}
                </button>
                <button
                  type="button"
                  className={styles.simSubTab}
                  data-active={simTab === 'trades'}
                  onClick={() => setSimTab('trades')}
                >
                  {t('welcome.sim.mock.tabTrades')}
                </button>
              </div>

              {simTab === 'perf' ? (
                <p className="caption">{t('welcome.sim.mock.note')}</p>
              ) : (
                <div className={styles.logList}>
                  <div className={styles.logItem}>{t('welcome.sim.mock.log1')}</div>
                  <div className={styles.logItem}>{t('welcome.sim.mock.log2')}</div>
                </div>
              )}
            </div>

            <Link to={pickPath} className={styles.ctaLink}>
              {t('welcome.sim.cta')}
            </Link>
          </>
        )}

        {activeFeature === 'tracker' && (
          <>
            <ul className={styles.pointsList}>
              <li>{t('welcome.tracker.point1')}</li>
              <li>{t('welcome.tracker.point2')}</li>
              <li>{t('welcome.tracker.point3')}</li>
            </ul>

            {/* Interactive Tracker Mock Widget */}
            <div className={styles.mockWidget} aria-label="Interactive live trade tracking preview">
              <div className={styles.mockHeader}>
                <div className={styles.liveBanner}>
                  <span className={styles.pulseDot} aria-hidden="true" />
                  <span>{t('welcome.tracker.mock.signalWaiting')}</span>
                </div>
                <span className={styles.badge} data-tone="live">
                  {t('welcome.tracker.mock.mode')}
                </span>
              </div>

              <div>
                <div className={styles.signalStock}>{t('welcome.tracker.mock.signalStock')}</div>
                <div className={styles.signalDetail}>{t('welcome.tracker.mock.signalDetail')}</div>
              </div>

              <div className={styles.signalActionCluster}>
                <span className={styles.paramLabel}>{t('welcome.tracker.mock.prompt')}</span>
                <button
                  type="button"
                  className={`${styles.signalBtn} ${styles.signalBtnDidIt}`}
                  onClick={() => setTrackerState('didIt')}
                >
                  {t('welcome.tracker.mock.btnDidIt')}
                </button>
                <button
                  type="button"
                  className={`${styles.signalBtn} ${styles.signalBtnSkipped}`}
                  onClick={() => setTrackerState('skipped')}
                >
                  {t('welcome.tracker.mock.btnSkipped')}
                </button>
                <button
                  type="button"
                  className={`${styles.signalBtn} ${styles.signalBtnSnooze}`}
                  onClick={() => setTrackerState('snooze')}
                >
                  {t('welcome.tracker.mock.btnSnooze')}
                </button>
              </div>

              {trackerState === 'didIt' && (
                <div className={styles.trackerStatus} role="status">
                  {t('welcome.tracker.mock.statusDidIt')}
                </div>
              )}
              {trackerState === 'skipped' && (
                <div className={styles.trackerStatus} role="status">
                  {t('welcome.tracker.mock.statusSkipped')}
                </div>
              )}
              {trackerState === 'snooze' && (
                <div className={styles.trackerStatus} role="status">
                  {t('welcome.tracker.mock.statusSnooze')}
                </div>
              )}

              <p className={styles.privacyCallout}>{t('welcome.tracker.mock.privacyCallout')}</p>
            </div>

            <Link to={pickPath} className={styles.ctaLink}>
              {t('welcome.tracker.cta')}
            </Link>
          </>
        )}
      </div>
    </section>
  )
}
