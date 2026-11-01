import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { pickPath } from '../new-strategy/paths'
import styles from './InteractiveFeatures.module.css'

type FeatureKey = 'builder' | 'simulation' | 'tracker'
type BuilderTemplateKey = 'discount' | 'comeback' | 'bear'
type TrackerActionKey = 'didIt' | 'skipped' | 'snooze'

interface Pillar {
  key: FeatureKey
  tagKey: MessageKey
  badgeKey: MessageKey
  badgeTone?: 'live'
  titleKey: MessageKey
  taglineKey: MessageKey
  summaryKey: MessageKey
  points: [MessageKey, MessageKey, MessageKey]
  ctaKey: MessageKey
}

const PILLARS: readonly Pillar[] = [
  {
    key: 'builder',
    tagKey: 'welcome.builder.tag',
    badgeKey: 'welcome.builder.badge',
    titleKey: 'welcome.builder.title',
    taglineKey: 'welcome.builder.tagline',
    summaryKey: 'welcome.builder.summary',
    points: ['welcome.builder.point1', 'welcome.builder.point2', 'welcome.builder.point3'],
    ctaKey: 'welcome.builder.cta',
  },
  {
    key: 'simulation',
    tagKey: 'welcome.sim.tag',
    badgeKey: 'welcome.sim.badge',
    titleKey: 'welcome.sim.title',
    taglineKey: 'welcome.sim.tagline',
    summaryKey: 'welcome.sim.summary',
    points: ['welcome.sim.point1', 'welcome.sim.point2', 'welcome.sim.point3'],
    ctaKey: 'welcome.sim.cta',
  },
  {
    key: 'tracker',
    tagKey: 'welcome.tracker.tag',
    badgeKey: 'welcome.tracker.badge',
    badgeTone: 'live',
    titleKey: 'welcome.tracker.title',
    taglineKey: 'welcome.tracker.tagline',
    summaryKey: 'welcome.tracker.summary',
    points: ['welcome.tracker.point1', 'welcome.tracker.point2', 'welcome.tracker.point3'],
    ctaKey: 'welcome.tracker.cta',
  },
] as const

const BUILDER_TEMPLATES: Record<
  BuilderTemplateKey,
  { nameKey: MessageKey; ruleKey: MessageKey; budgetKey: MessageKey }
> = {
  discount: {
    nameKey: 'welcome.builder.mock.t1Name',
    ruleKey: 'welcome.builder.mock.t1Rule',
    budgetKey: 'welcome.builder.mock.t1Budget',
  },
  comeback: {
    nameKey: 'welcome.builder.mock.t2Name',
    ruleKey: 'welcome.builder.mock.t2Rule',
    budgetKey: 'welcome.builder.mock.t2Budget',
  },
  bear: {
    nameKey: 'welcome.builder.mock.t3Name',
    ruleKey: 'welcome.builder.mock.t3Rule',
    budgetKey: 'welcome.builder.mock.t3Budget',
  },
}

const WATCHED_STOCKS = ['AAPL ✓', 'MSFT ✓', 'NVDA ✓', 'AMZN ✓']

const SIM_STATS: Array<{
  labelKey: MessageKey
  value: string
  valueClass: string
}> = [
  { labelKey: 'welcome.sim.mock.returnLabel', value: '+14.2%', valueClass: styles.statPositive },
  { labelKey: 'welcome.sim.mock.indexLabel', value: '+8.1%', valueClass: styles.statIndex },
  { labelKey: 'welcome.sim.mock.worstLabel', value: '-12.4%', valueClass: styles.statNegative },
]

const PAPER_LOGS: MessageKey[] = ['welcome.sim.mock.log1', 'welcome.sim.mock.log2']

const TRACKER_ACTIONS: Array<{
  key: TrackerActionKey
  labelKey: MessageKey
  className: string
  statusKey: MessageKey
}> = [
  {
    key: 'didIt',
    labelKey: 'welcome.tracker.mock.btnDidIt',
    className: styles.signalBtnDidIt,
    statusKey: 'welcome.tracker.mock.statusDidIt',
  },
  {
    key: 'skipped',
    labelKey: 'welcome.tracker.mock.btnSkipped',
    className: styles.signalBtnSkipped,
    statusKey: 'welcome.tracker.mock.statusSkipped',
  },
  {
    key: 'snooze',
    labelKey: 'welcome.tracker.mock.btnSnooze',
    className: styles.signalBtnSnooze,
    statusKey: 'welcome.tracker.mock.statusSnooze',
  },
]

export function InteractiveFeatures() {
  const t = useT()
  const [activeFeature, setActiveFeature] = useState<FeatureKey>('builder')
  const [builderTemplate, setBuilderTemplate] = useState<BuilderTemplateKey>('discount')
  const [simTab, setSimTab] = useState<'perf' | 'trades'>('perf')
  const [trackerState, setTrackerState] = useState<TrackerActionKey | null>(null)

  const activePillar = PILLARS.find((p) => p.key === activeFeature) ?? PILLARS[0]
  const currentTrackerAction = trackerState
    ? TRACKER_ACTIONS.find((a) => a.key === trackerState) ?? null
    : null

  const handleTabKeyDown = (e: React.KeyboardEvent, index: number) => {
    let nextIndex = -1
    if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
      nextIndex = (index + 1) % PILLARS.length
    } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
      nextIndex = (index - 1 + PILLARS.length) % PILLARS.length
    }
    if (nextIndex >= 0) {
      e.preventDefault()
      const nextKey = PILLARS[nextIndex].key
      setActiveFeature(nextKey)
      const nextTabEl = document.getElementById(`tab-${nextKey}`)
      nextTabEl?.focus()
    }
  }

  return (
    <section className={styles.container} aria-labelledby="features-heading">
      <div className={styles.cardList} role="tablist" aria-label={t('welcome.features.title')}>
        {PILLARS.map((pillar, index) => {
          const isActive = activeFeature === pillar.key
          return (
            <button
              key={pillar.key}
              type="button"
              role="tab"
              id={`tab-${pillar.key}`}
              aria-selected={isActive}
              aria-controls="panel-feature-detail"
              className={styles.featureCard}
              data-active={isActive}
              onClick={() => setActiveFeature(pillar.key)}
              onKeyDown={(e) => handleTabKeyDown(e, index)}
            >
              <div className={styles.cardTop}>
                <span className={styles.tag}>{t(pillar.tagKey)}</span>
                <span className={styles.badge} data-tone={pillar.badgeTone}>
                  {t(pillar.badgeKey)}
                </span>
              </div>
              <h3 className={styles.cardTitle}>{t(pillar.titleKey)}</h3>
              <p className={styles.tagline}>"{t(pillar.taglineKey)}"</p>
              <p className={styles.summary}>{t(pillar.summaryKey)}</p>
              <div className={styles.expandToggle}>
                <span>{isActive ? t('welcome.collapse') : t('welcome.expand')}</span>
              </div>
            </button>
          )
        })}
      </div>

      {/* Expanded Interactive Detail Panel */}
      <div
        id="panel-feature-detail"
        role="tabpanel"
        aria-labelledby={`tab-${activeFeature}`}
        className={styles.detailPanel}
      >
        <ul className={styles.pointsList}>
          {activePillar.points.map((pointKey) => (
            <li key={pointKey}>{t(pointKey)}</li>
          ))}
        </ul>

        {activeFeature === 'builder' && (
          <div className={styles.mockWidget} aria-label="Interactive strategy builder preview">
            <div className={styles.mockHeader}>
              <span className={styles.mockLabel}>{t('welcome.builder.mock.templateLabel')}</span>
              <span className={styles.tag}>STL // TEMPLATE_ENGINE</span>
            </div>

            <div className={styles.templatePills}>
              {(Object.keys(BUILDER_TEMPLATES) as BuilderTemplateKey[]).map((key) => (
                <button
                  key={key}
                  type="button"
                  className={styles.templatePill}
                  data-active={builderTemplate === key}
                  aria-pressed={builderTemplate === key}
                  onClick={() => setBuilderTemplate(key)}
                >
                  {t(BUILDER_TEMPLATES[key].nameKey)}
                </button>
              ))}
            </div>

            <div className={styles.ruleParams}>
              <div className={styles.paramBlock}>
                <span className={styles.paramLabel}>{t('welcome.builder.mock.ruleLabel')}</span>
                <span className={styles.paramValue}>{t(BUILDER_TEMPLATES[builderTemplate].ruleKey)}</span>
              </div>
              <div className={styles.paramBlock}>
                <span className={styles.paramLabel}>{t('welcome.builder.mock.budgetLabel')}</span>
                <span className={styles.paramValue}>{t(BUILDER_TEMPLATES[builderTemplate].budgetKey)}</span>
              </div>
            </div>

            <div className={styles.stockRow}>
              <span className={styles.paramLabel}>{t('welcome.builder.mock.stocksLabel')}:</span>
              {WATCHED_STOCKS.map((stock) => (
                <span key={stock} className={styles.stockBadge}>
                  {stock}
                </span>
              ))}
            </div>
          </div>
        )}

        {activeFeature === 'simulation' && (
          <div className={styles.mockWidget} aria-label="Interactive paper trading simulation preview">
            <div className={styles.mockHeader}>
              <span className={styles.badge}>{t('welcome.sim.mock.mode')}</span>
              <span className={styles.tag}>STL // FORWARD_TEST</span>
            </div>

            <div className={styles.simStats}>
              {SIM_STATS.map(({ labelKey, value, valueClass }) => (
                <div key={labelKey} className={styles.statBox}>
                  <span className={styles.statLabel}>{t(labelKey)}</span>
                  <span className={`${styles.statValue} ${valueClass}`}>{value}</span>
                </div>
              ))}
            </div>

            <div className={styles.simSubTabs} role="tablist" aria-label={t('welcome.sim.title')}>
              <button
                type="button"
                role="tab"
                id="tab-sim-perf"
                aria-selected={simTab === 'perf'}
                aria-controls="panel-sim-content"
                className={styles.simSubTab}
                data-active={simTab === 'perf'}
                onClick={() => setSimTab('perf')}
              >
                {t('welcome.sim.mock.tabPerf')}
              </button>
              <button
                type="button"
                role="tab"
                id="tab-sim-trades"
                aria-selected={simTab === 'trades'}
                aria-controls="panel-sim-content"
                className={styles.simSubTab}
                data-active={simTab === 'trades'}
                onClick={() => setSimTab('trades')}
              >
                {t('welcome.sim.mock.tabTrades')}
              </button>
            </div>

            <div
              id="panel-sim-content"
              role="tabpanel"
              aria-labelledby={simTab === 'perf' ? 'tab-sim-perf' : 'tab-sim-trades'}
            >
              {simTab === 'perf' ? (
                <p className="caption">{t('welcome.sim.mock.note')}</p>
              ) : (
                <div className={styles.logList}>
                  {PAPER_LOGS.map((logKey) => (
                    <div key={logKey} className={styles.logItem}>
                      {t(logKey)}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {activeFeature === 'tracker' && (
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
              {TRACKER_ACTIONS.map(({ key, labelKey, className }) => (
                <button
                  key={key}
                  type="button"
                  className={`${styles.signalBtn} ${className}`}
                  aria-pressed={trackerState === key}
                  onClick={() => setTrackerState(key)}
                >
                  {t(labelKey)}
                </button>
              ))}
            </div>

            {currentTrackerAction && (
              <div className={styles.trackerStatus} role="status">
                {t(currentTrackerAction.statusKey)}
              </div>
            )}

            <p className={styles.privacyCallout}>{t('welcome.tracker.mock.privacyCallout')}</p>
          </div>
        )}

        <Link to={pickPath} className={styles.ctaLink}>
          {t(activePillar.ctaKey)}
        </Link>
      </div>
    </section>
  )
}
