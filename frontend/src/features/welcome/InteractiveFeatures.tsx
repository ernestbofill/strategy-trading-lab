import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Card } from '../../components/Card'
import { InfoTip } from '../../components/InfoTip'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { signedPct } from '../../lib/format'
import { pickPath } from '../new-strategy/paths'
import styles from './InteractiveFeatures.module.css'

type FeatureKey = 'builder' | 'simulation' | 'tracker'
type BuilderTemplateKey = 'discount' | 'comeback' | 'bear'
type TrackerActionKey = 'didIt' | 'skipped' | 'snooze'

interface Pillar {
  key: FeatureKey
  tagKey: MessageKey
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
    titleKey: 'welcome.builder.title',
    taglineKey: 'welcome.builder.tagline',
    summaryKey: 'welcome.builder.summary',
    points: ['welcome.builder.point1', 'welcome.builder.point2', 'welcome.builder.point3'],
    ctaKey: 'welcome.builder.cta',
  },
  {
    key: 'simulation',
    tagKey: 'welcome.sim.tag',
    titleKey: 'welcome.sim.title',
    taglineKey: 'welcome.sim.tagline',
    summaryKey: 'welcome.sim.summary',
    points: ['welcome.sim.point1', 'welcome.sim.point2', 'welcome.sim.point3'],
    ctaKey: 'welcome.sim.cta',
  },
  {
    key: 'tracker',
    tagKey: 'welcome.tracker.tag',
    titleKey: 'welcome.tracker.title',
    taglineKey: 'welcome.tracker.tagline',
    summaryKey: 'welcome.tracker.summary',
    points: ['welcome.tracker.point1', 'welcome.tracker.point2', 'welcome.tracker.point3'],
    ctaKey: 'welcome.tracker.cta',
  },
] as const

const BUILDER_TEMPLATES: Record<
  BuilderTemplateKey,
  { nameKey: MessageKey; ruleKey: MessageKey; budgetKey: MessageKey; assetType: 'stocks' | 'fund' }
> = {
  discount: {
    nameKey: 'welcome.builder.mock.t1Name',
    ruleKey: 'welcome.builder.mock.t1Rule',
    budgetKey: 'welcome.builder.mock.t1Budget',
    assetType: 'stocks',
  },
  comeback: {
    nameKey: 'welcome.builder.mock.t2Name',
    ruleKey: 'welcome.builder.mock.t2Rule',
    budgetKey: 'welcome.builder.mock.t2Budget',
    assetType: 'stocks',
  },
  bear: {
    nameKey: 'welcome.builder.mock.t3Name',
    ruleKey: 'welcome.builder.mock.t3Rule',
    budgetKey: 'welcome.builder.mock.t3Budget',
    assetType: 'fund',
  },
}

const WATCHED_STOCKS = ['ZS \u2713', 'AAPL \u2713', 'MSFT \u2713', 'NVDA \u2713']

const SIM_STATS: Array<{
  labelKey: MessageKey
  value: string
  valueClass: string
  hasInfoTip?: boolean
}> = [
  { labelKey: 'welcome.sim.mock.returnLabel', value: signedPct(14.2, 1), valueClass: styles.statPositive },
  {
    labelKey: 'welcome.sim.mock.indexLabel',
    value: signedPct(8.1, 1),
    valueClass: styles.statIndex,
    hasInfoTip: true,
  },
  { labelKey: 'welcome.sim.mock.worstLabel', value: signedPct(-12.4, 1), valueClass: styles.statNegative },
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
  const [activeFeature, setActiveFeature] = useState<FeatureKey | null>(null)
  const [builderTemplate, setBuilderTemplate] = useState<BuilderTemplateKey>('discount')
  const [simTab, setSimTab] = useState<'perf' | 'trades'>('perf')
  const [trackerState, setTrackerState] = useState<TrackerActionKey | null>(null)

  const activePillar = activeFeature ? (PILLARS.find((p) => p.key === activeFeature) ?? null) : null
  const currentTrackerAction = trackerState
    ? (TRACKER_ACTIONS.find((a) => a.key === trackerState) ?? null)
    : null

  const handleTabKeyDown = (e: React.KeyboardEvent, index: number) => {
    let nextIndex = -1
    if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
      nextIndex = (index + 1) % PILLARS.length
    } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
      nextIndex = (index - 1 + PILLARS.length) % PILLARS.length
    } else if (e.key === 'Home') {
      nextIndex = 0
    } else if (e.key === 'End') {
      nextIndex = PILLARS.length - 1
    }
    if (nextIndex >= 0) {
      e.preventDefault()
      const nextKey = PILLARS[nextIndex].key
      setActiveFeature(nextKey)
      document.getElementById(`tab-${nextKey}`)?.focus()
    }
  }

  const handleSimSubTabKeyDown = (e: React.KeyboardEvent, current: 'perf' | 'trades') => {
    if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
      e.preventDefault()
      const next = current === 'perf' ? 'trades' : 'perf'
      setSimTab(next)
      document.getElementById(`tab-sim-${next}`)?.focus()
    }
  }

  const selectedTemplate = BUILDER_TEMPLATES[builderTemplate]

  return (
    <div className={styles.container}>
      <div className={styles.cardList} role="tablist" aria-label={t('welcome.features.title')}>
        {PILLARS.map((pillar, index) => {
          const isActive = activeFeature === pillar.key
          const isTabbable = isActive || (activeFeature === null && index === 0)
          return (
            <Card
              as="button"
              key={pillar.key}
              type="button"
              role="tab"
              id={`tab-${pillar.key}`}
              tabIndex={isTabbable ? 0 : -1}
              aria-selected={isActive}
              aria-expanded={isActive}
              aria-controls="panel-feature-detail"
              className={styles.featureCard}
              data-feature={pillar.key}
              interactive
              selected={isActive}
              onClick={() => setActiveFeature((prev) => (prev === pillar.key ? null : pillar.key))}
              onKeyDown={(e) => handleTabKeyDown(e, index)}
            >
              <span className={styles.tag}>{t(pillar.tagKey)}</span>
              <h3 className={styles.cardTitle}>{t(pillar.titleKey)}</h3>
              <p className={styles.tagline}>"{t(pillar.taglineKey)}"</p>
              <p className={styles.summary}>{t(pillar.summaryKey)}</p>
              <div className={styles.expandToggle}>
                <span>{isActive ? t('welcome.collapse') : t('welcome.expand')}</span>
              </div>
            </Card>
          )
        })}
      </div>

      {/* Expanded Interactive Detail Panel */}
      {activePillar && (
        <div
          id="panel-feature-detail"
          role="tabpanel"
          aria-labelledby={`tab-${activePillar.key}`}
          className={styles.detailPanel}
          data-feature={activePillar.key}
        >
          <ul className={styles.pointsList}>
            {activePillar.points.map((pointKey) => (
              <li key={pointKey}>{t(pointKey)}</li>
            ))}
          </ul>

          {activePillar.key === 'builder' && (
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
                  <span className={styles.paramValue}>{t(selectedTemplate.ruleKey)}</span>
                </div>
                <div className={styles.paramBlock}>
                  <span className={styles.paramLabel}>{t('welcome.builder.mock.budgetLabel')}</span>
                  <span className={styles.paramValue}>{t(selectedTemplate.budgetKey)}</span>
                </div>
              </div>

              <div className={styles.stockRow}>
                {selectedTemplate.assetType === 'fund' ? (
                  <>
                    <span className={styles.paramLabel}>{t('welcome.builder.mock.fundLabel')}:</span>
                    <span className={styles.stockBadge}>{t('welcome.builder.mock.bearFund')}</span>
                  </>
                ) : (
                  <>
                    <span className={styles.paramLabel}>{t('welcome.builder.mock.stocksLabel')}:</span>
                    {WATCHED_STOCKS.map((stock) => (
                      <span key={stock} className={styles.stockBadge}>
                        {stock}
                      </span>
                    ))}
                  </>
                )}
              </div>
            </div>
          )}

          {activePillar.key === 'simulation' && (
            <div className={styles.mockWidget} aria-label="Interactive paper trading simulation preview">
              <div className={styles.mockHeader}>
                <span className={styles.badge}>{t('welcome.sim.mock.mode')}</span>
                <span className={styles.tag}>STL // FORWARD_TEST</span>
              </div>

              <div className={styles.simStats}>
                {SIM_STATS.map(({ labelKey, value, valueClass, hasInfoTip }) => (
                  <div key={labelKey} className={styles.statBox}>
                    <span className={styles.statLabel}>
                      {t(labelKey)}
                      {hasInfoTip && (
                        <InfoTip label={t('common.indexTip.label')} text={t('common.indexTip.text')} />
                      )}
                    </span>
                    <span className={`${styles.statValue} ${valueClass}`}>{value}</span>
                  </div>
                ))}
              </div>

              <div className={styles.simSubTabs} role="tablist" aria-label={t('welcome.sim.title')}>
                <button
                  type="button"
                  role="tab"
                  id="tab-sim-perf"
                  tabIndex={simTab === 'perf' ? 0 : -1}
                  aria-selected={simTab === 'perf'}
                  aria-controls="panel-sim-content"
                  className={styles.simSubTab}
                  data-active={simTab === 'perf'}
                  onClick={() => setSimTab('perf')}
                  onKeyDown={(e) => handleSimSubTabKeyDown(e, 'perf')}
                >
                  {t('welcome.sim.mock.tabPerf')}
                </button>
                <button
                  type="button"
                  role="tab"
                  id="tab-sim-trades"
                  tabIndex={simTab === 'trades' ? 0 : -1}
                  aria-selected={simTab === 'trades'}
                  aria-controls="panel-sim-content"
                  className={styles.simSubTab}
                  data-active={simTab === 'trades'}
                  onClick={() => setSimTab('trades')}
                  onKeyDown={(e) => handleSimSubTabKeyDown(e, 'trades')}
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

          {activePillar.key === 'tracker' && (
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
      )}
    </div>
  )
}
