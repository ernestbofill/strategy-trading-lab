import { chartLines } from '../lib/sparkline'
import styles from './TwoLineChart.module.css'

const WIDTH = 326

/**
 * A small chart of two lines over the same days, the first solid and the second dashed, with their names under it (a strategy's
 * results against the S&P 500 fund). Decorative: the figures beside it say the same in words. A null value leaves a gap.
 */
export function TwoLineChart({ first, second, labels, height = 56 }: {
  first: (number | null)[]
  second: (number | null)[] | null
  labels: [string, string]
  height?: number
}) {
  const [one, two] = chartLines([first, second ?? []], WIDTH, height)
  if (!one) return null
  return (
    <div className={styles.chart}>
      <svg width="100%" height={height} viewBox={`0 0 ${WIDTH} ${height}`} preserveAspectRatio="none" aria-hidden="true">
        <line x1="0" y1={height - 1} x2={WIDTH} y2={height - 1} className={styles.axis} />
        {two && <polyline points={two} className={styles.second} vectorEffect="non-scaling-stroke" />}
        <polyline points={one} className={styles.first} vectorEffect="non-scaling-stroke" />
      </svg>
      <div className={styles.legend} aria-hidden="true">
        <span>
          <span className={styles.keyFirst}>━</span> {labels[0]}
        </span>
        {two && (
          <span>
            <span className={styles.keySecond}>┅</span> {labels[1]}
          </span>
        )}
      </div>
    </div>
  )
}
