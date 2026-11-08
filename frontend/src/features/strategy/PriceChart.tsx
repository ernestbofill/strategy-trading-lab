import { useState } from 'react'
import { api, type CheckTexts, type StrategyTradeItem } from '../../api'
import { SegmentedField } from '../../components/Field'
import { useT } from '../../i18n/I18n'
import { exact, shortDate } from '../../lib/format'
import { useLoadable } from '../../lib/useLoadable'
import { chartSeries, isCandidateOn, lastDays, priceRange, segments, spreadLabels } from './priceChartMarks'
import styles from './PriceChart.module.css'

const WIDTH = 340
const HEIGHT = 160
const PERIODS = ['90', '30', '7']
/** A right-axis label's height, in the plot's pixels: labels sit at least this far apart. */
const LABEL = 16

/**
 * The stock's closes with the rules' levels of each day drawn over them, as they moved (spec: Stock page). Before the
 * strategy started only what makes a candidate shows, since buys don't apply yet; a marker shows the start, and dots its
 * trades. Every day draws the levels of the stock's verdict today. Not interactive: the right axis gives the latest price
 * and each level's latest value. Green for a candidate, violet for a stock that isn't one.
 */
export function PriceChart({
  strategyId,
  assetId,
  isCandidate,
  currency,
  trades,
  texts,
}: {
  strategyId: string
  assetId: string
  /** The stored verdict the page shows; null before the daily check has stored any. */
  isCandidate: boolean | null
  currency: string
  trades: StrategyTradeItem[]
  texts: Record<string, CheckTexts> | null | undefined
}) {
  const t = useT()
  const [days, setDays] = useState('30')
  const chart = useLoadable(`chart:${strategyId}:${assetId}`, () => api.getStockChart(strategyId, assetId))
  if (chart.status !== 'ready' || chart.data.days.length < 2) return null
  // A short period with a gap in the closes still draws its last two, so the buttons stay; the dates under it say when.
  const period = lastDays(chart.data.days, Number(days))
  const points = period.length >= 2 ? period : chart.data.days.slice(-2)
  const first = points[0].date
  const last = points[points.length - 1]
  // The page's verdict, as the header's status says; a stock the daily check hasn't seen yet takes the latest day's.
  const candidate = isCandidate ?? isCandidateOn(last.checks)

  const series = chartSeries(points, candidate)
  // The axis's levels are the latest day the rules checked, which can be a day behind the latest close.
  const lastChecked = points.findLastIndex((p) => p.checks.length > 0)
  const dayOf = (date: string) => points.findIndex((p) => p.date >= date)
  // On the price line at its day's close: a trade's own price is what was paid, which a later split no longer matches.
  const dots = trades.filter((tr) => tr.tradeDate >= first && tr.tradeDate <= last.date).map((tr) => ({ ...tr, i: dayOf(tr.tradeDate) }))
  const startedOn = chart.data.startedOn
  const started = startedOn && startedOn > first && startedOn <= last.date ? dayOf(startedOn) : null

  const [low, high] = priceRange([
    ...points.map((p) => p.close),
    ...series.flatMap((s) => s.points.flatMap((p) => (p.to === undefined ? [p.from] : [p.from, p.to]))),
  ])
  const step = WIDTH / (points.length - 1)
  const x = (i: number) => +(i * step).toFixed(1)
  const y = (price: number) => +(HEIGHT - ((price - low) / (high - low)) * HEIGHT).toFixed(1)
  // A level shown on a single day still gets a short dash.
  const xs = (run: { i: number }[]) =>
    run.length === 1 ? [Math.max(0, x(run[0].i) - step / 2), Math.min(WIDTH, x(run[0].i) + step / 2)] : run.map((p) => x(p.i))
  const pct = (value: number, of: number) => `${(value / of) * 100}%`
  const nameOf = (s: (typeof series)[number]) => (s.mark.check ? (texts?.[s.mark.check]?.label ?? s.mark.check) : t(s.mark.label!))

  // The right axis: the latest close, then each level's latest value (both edges of an area).
  const labels = [
    { key: 'price', tone: 'price', value: last.close },
    ...series.flatMap((s) => {
      const today = s.points[s.points.length - 1]
      if (today.i !== lastChecked) return []
      const values = today.to === undefined ? [today.from] : [today.from, today.to]
      return values.map((value, k) => ({ key: `${s.mark.id}:${k}`, tone: s.mark.tone, value }))
    }),
  ]
  const labelTops = spreadLabels(labels.map((l) => y(l.value)), LABEL, LABEL / 2, HEIGHT - LABEL / 2)

  return (
    <div className={styles.chart} data-candidate={candidate}>
      <SegmentedField
        label={t('stock.chart.period')}
        hideLabel
        compact
        value={days}
        onChange={setDays}
        options={PERIODS.map((d) => ({ value: d, label: t('stock.chart.days', { days: d }) }))}
      />
      <div className={styles.frame}>
        <div className={styles.plot}>
          <svg width="100%" height={HEIGHT} viewBox={`0 0 ${WIDTH} ${HEIGHT}`} preserveAspectRatio="none" aria-hidden="true">
            {started !== null && <line x1={x(started)} x2={x(started)} y1="0" y2={HEIGHT} className={styles.start} vectorEffect="non-scaling-stroke" />}
            {series.flatMap((s) =>
              segments(s.points).map((run) => {
                const at = xs(run)
                const key = `${s.mark.id}:${run[0].i}`
                const tops = run.length === 1 ? [run[0], run[0]] : run
                if (!s.area)
                  return <polyline key={key} points={at.map((px, k) => `${px},${y(tops[k].from)}`).join(' ')} className={styles.level} data-tone={s.mark.tone} vectorEffect="non-scaling-stroke" />
                const edge = [...at.map((px, k) => `${px},${y(tops[k].from)}`), ...at.map((px, k) => `${px},${y(tops[k].to!)}`).reverse()]
                return <polygon key={key} points={edge.join(' ')} className={styles.area} data-tone={s.mark.tone} vectorEffect="non-scaling-stroke" />
              }),
            )}
            <polyline points={points.map((p, i) => `${x(i)},${y(p.close)}`).join(' ')} className={styles.price} vectorEffect="non-scaling-stroke" />
          </svg>
          {/* Round marks in HTML, so the stretched SVG doesn't squash them. */}
          <span className={styles.dot} style={{ left: '100%', top: pct(y(last.close), HEIGHT) }} />
          {dots.map((d) => (
            <span key={d.id} className={styles.trade} data-side={d.side} style={{ left: pct(x(d.i), WIDTH), top: pct(y(points[d.i].close), HEIGHT) }} />
          ))}
          {started !== null && (
            <span className={styles.startLabel} data-left={x(started) > WIDTH * 0.6} style={{ left: pct(x(started), WIDTH) }}>
              {t('stock.chart.started', { date: shortDate(startedOn!) })}
            </span>
          )}
        </div>
        <div className={styles.axis}>
          {labels.map((l, k) => (
            <span key={l.key} className={styles.axisLabel} data-tone={l.tone} style={{ top: labelTops[k] }}>
              {exact(l.value, currency)}
            </span>
          ))}
        </div>
        <div className={styles.dates} aria-hidden="true">
          <span>{shortDate(first)}</span>
          <span>{shortDate(last.date)}</span>
        </div>
      </div>
      <ul className={styles.legend}>
        <li>
          <span className={styles.key} data-kind="price" />
          {t('stock.chart.price')}
        </li>
        {series.map((s) => (
          <li key={s.mark.id}>
            <span className={styles.key} data-kind={s.area ? 'area' : 'line'} data-tone={s.mark.tone} />
            {nameOf(s)}
          </li>
        ))}
        {(['buy', 'sell'] as const)
          .filter((side) => dots.some((d) => d.side === side))
          .map((side) => (
            <li key={side}>
              <span className={styles.tradeKey} data-side={side} />
              {t(side === 'buy' ? 'stock.chart.buy' : 'stock.chart.sell')}
            </li>
          ))}
      </ul>
    </div>
  )
}
