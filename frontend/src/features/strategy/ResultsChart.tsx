import type { ResultPoint } from '../../api'
import { TwoLineChart } from '../../components/TwoLineChart'
import { useT } from '../../i18n/I18n'

/** The results over time: You (live) or Since start, and the S&P 500 fund (dashed). The figures above say the same in words. */
export function ResultsChart({ history, live }: { history: ResultPoint[]; live: boolean }) {
  const t = useT()
  return (
    <TwoLineChart
      first={history.map((p) => p.you)}
      second={history.map((p) => p.index)}
      labels={[live ? t('strategy.figure.you') : t('strategy.figure.sinceStart'), t('strategy.figure.index')]}
    />
  )
}
