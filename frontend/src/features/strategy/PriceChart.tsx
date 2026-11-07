import { useState } from 'react'
import { api, type RuleCheck } from '../../api'
import { SegmentedField } from '../../components/Field'
import { useT } from '../../i18n/I18n'
import { exact, shortDate } from '../../lib/format'
import { useLoadable } from '../../lib/useLoadable'
import { chartMarks, lastDays, priceRange } from './priceChartMarks'
import styles from './PriceChart.module.css'

const WIDTH = 340
const HEIGHT = 160
const PERIODS = ['90', '30', '7']

/**
 * The stock's closes with the rules' levels of the latest check drawn over them (spec: Stock page). Not interactive:
 * the legend gives each level's price. Green for a candidate, violet for a stock that isn't one.
 */
export function PriceChart({
  strategyId,
  assetId,
  checks,
  isCandidate,
  currency,
}: {
  strategyId: string
  assetId: string
  checks: RuleCheck[]
  isCandidate: boolean
  currency: string
}) {
  const t = useT()
  const [days, setDays] = useState('30')
  const prices = useLoadable(`prices:${strategyId}:${assetId}`, () => api.getStockPrices(strategyId, assetId))
  if (prices.status !== 'ready' || prices.data.length < 2) return null
  // A short period with a gap in the closes still draws its last two, so the buttons stay; the dates under it say when.
  const period = lastDays(prices.data, Number(days))
  const points = period.length >= 2 ? period : prices.data.slice(-2)

  const marks = chartMarks(checks, isCandidate)
  const [low, high] = priceRange(points, marks)
  const y = (price: number) => +(HEIGHT - ((price - low) / (high - low)) * HEIGHT).toFixed(1)
  const x = (i: number) => +((i / (points.length - 1)) * WIDTH).toFixed(1)
  const line = points.map((p, i) => `${x(i)},${y(p.close)}`).join(' ')
  const last = points[points.length - 1]

  return (
    <div className={styles.chart} data-candidate={isCandidate}>
      <SegmentedField
        label={t('stock.chart.period')}
        hideLabel
        value={days}
        onChange={setDays}
        options={PERIODS.map((d) => ({ value: d, label: t('stock.chart.days', { days: d }) }))}
      />
      <div className={styles.plot}>
        <svg width="100%" height={HEIGHT} viewBox={`0 0 ${WIDTH} ${HEIGHT}`} preserveAspectRatio="none" aria-hidden="true">
          {marks.map((m) =>
            m.to === undefined ? (
              <line key={m.label} x1="0" x2={WIDTH} y1={y(m.from)} y2={y(m.from)} className={styles.level} data-tone={m.tone} vectorEffect="non-scaling-stroke" />
            ) : (
              <rect
                key={m.label}
                x="0"
                width={WIDTH}
                y={Math.min(y(m.from), y(m.to))}
                height={Math.abs(y(m.from) - y(m.to))}
                className={styles.area}
                data-tone={m.tone}
                vectorEffect="non-scaling-stroke"
              />
            ),
          )}
          <polyline points={line} className={styles.price} vectorEffect="non-scaling-stroke" />
        </svg>
        <span className={styles.dot} style={{ top: `${(y(last.close) / HEIGHT) * 100}%` }} />
      </div>
      <div className={styles.dates} aria-hidden="true">
        <span>{shortDate(points[0].date)}</span>
        <span>{shortDate(last.date)}</span>
      </div>
      <ul className={styles.legend}>
        <li>
          <span className={styles.key} data-kind="price" />
          {t('stock.chart.price')}
        </li>
        {marks.map((m) => (
          <li key={m.label}>
            <span className={styles.key} data-kind={m.to === undefined ? 'line' : 'area'} data-tone={m.tone} />
            {t(m.label)}{' '}
            <span className={styles.value}>
              {m.to === undefined ? exact(m.from, currency) : `${exact(Math.min(m.from, m.to), currency)}–${exact(Math.max(m.from, m.to), currency)}`}
            </span>
          </li>
        ))}
      </ul>
      {marks.length > 0 && <p className={styles.note}>{t('stock.chart.today')}</p>}
    </div>
  )
}
