import type { ResultPoint } from '../../api'
import { useT } from '../../i18n/I18n'
import { chartLines } from '../../lib/sparkline'
import styles from './StrategyPage.module.css'

const WIDTH = 326
const HEIGHT = 56

/** The results over time: You (live) or Since start, the rules alone (live) and the S&P 500 fund (dashed). The figures above say the same in words. */
export function ResultsChart({ history, live }: { history: ResultPoint[]; live: boolean }) {
  const t = useT()
  const [you, rules, index] = chartLines(
    [history.map((p) => p.you), history.map((p) => (live ? p.rules : null)), history.map((p) => p.index)],
    WIDTH,
    HEIGHT,
  )
  if (!you) return null
  return (
    <div className={styles.chart}>
      <svg width="100%" height={HEIGHT} viewBox={`0 0 ${WIDTH} ${HEIGHT}`} preserveAspectRatio="none" aria-hidden="true">
        <line x1="0" y1={HEIGHT - 1} x2={WIDTH} y2={HEIGHT - 1} className={styles.axis} />
        {index && <polyline points={index} className={styles.lineIndex} vectorEffect="non-scaling-stroke" />}
        {rules && <polyline points={rules} className={styles.lineRules} vectorEffect="non-scaling-stroke" />}
        <polyline points={you} className={styles.lineYou} vectorEffect="non-scaling-stroke" />
      </svg>
      <div className={styles.legend} aria-hidden="true">
        <span>
          <span className={styles.keyYou}>━</span> {live ? t('strategy.figure.you') : t('strategy.figure.sinceStart')}
        </span>
        {live && (
          <span>
            <span className={styles.keyRules}>━</span> {t('strategy.figure.rules')}
          </span>
        )}
        <span>
          <span className={styles.keyIndex}>┅</span> {t('strategy.figure.index')}
        </span>
      </div>
    </div>
  )
}
