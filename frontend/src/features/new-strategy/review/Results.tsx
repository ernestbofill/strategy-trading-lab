import { useId, useState } from 'react'
import type { BenchmarkBacktest, PeriodResult } from '../../../api'
import { InfoTip } from '../../../components/InfoTip'
import { Tabs } from '../../../components/Tabs'
import { tabPanel } from '../../../components/tabPanel'
import { useT } from '../../../i18n/I18n'
import { signedPct } from '../../../lib/format'
import styles from './Results.module.css'

/** A result in percent; the sign is always written, colour only repeats it. */
function Pct({
  value,
  plain,
  lessBad,
  digits,
}: {
  value: number
  plain?: boolean
  lessBad?: boolean
  digits?: number
}) {
  const sign = plain ? undefined : value < 0 ? (lessBad ? 'warn' : 'neg') : 'pos'
  return (
    <span className={styles.pct} data-sign={sign}>
      {signedPct(value, digits)}
    </span>
  )
}

/** The Tested box's figures: the template next to the S&P 500 fund, a year on average and in the worst year. */
export function ResultFigures({ result }: { result: PeriodResult }) {
  const t = useT()
  const worst = result.worstYear ?? 0
  const indexWorst = result.indexWorstYear ?? 0
  return (
    <table className={styles.figures}>
      <thead>
        <tr>
          <td />
          <th scope="col">{t('newStrategy.results.thisTemplate')}</th>
          <th scope="col">
            {t('newStrategy.results.indexFund')}
            <InfoTip label={t('common.indexTip.label')} text={t('common.indexTip.text')} />
          </th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <th scope="row">{t('newStrategy.results.perYear')}</th>
          <td>
            <Pct value={result.templatePerYear ?? 0} digits={1} />
          </td>
          <td>
            <Pct value={result.indexPerYear ?? 0} digits={1} plain />
          </td>
        </tr>
        <tr>
          <th scope="row">{t('newStrategy.results.worstYear')}</th>
          <td>
            <Pct value={worst} lessBad={worst > indexWorst} />
          </td>
          <td>
            <Pct value={indexWorst} lessBad={indexWorst > worst} />
          </td>
        </tr>
      </tbody>
    </table>
  )
}

/** "By period" table on How we tested: a year on average and the worst year, the template next to the index, plus alpha and optional story. */
export function PeriodTable({
  periods,
  benchmarks,
}: {
  periods: PeriodResult[]
  benchmarks?: BenchmarkBacktest[] | null
}) {
  const t = useT()
  const tabsId = useId()
  const [activeKey, setActiveKey] = useState(benchmarks?.[0]?.key)

  const activeBenchmark = benchmarks?.find((b) => b.key === activeKey) ?? benchmarks?.[0]
  const activePeriods = activeBenchmark?.periods ?? periods
  const indexLabel = activeBenchmark?.name ?? t('newStrategy.results.index')

  const hasAlpha = activePeriods.some((p) => p.alpha !== undefined && p.alpha !== null)
  const hasStory = activePeriods.some((p) => p.templatePerYear !== null && Boolean(p.story))
  const dataCols = 4 + (hasAlpha ? 1 : 0) + (hasStory ? 1 : 0)

  const pair = (
    <>
      <th scope="col">{t('newStrategy.results.template')}</th>
      <th scope="col">{indexLabel}</th>
    </>
  )

  const table = (
    <div
      className={`well ${styles.tableWrap}`}
      {...(activeBenchmark ? tabPanel(tabsId, activeBenchmark.key, true) : {})}
    >
      <table className={styles.periods}>
        <thead>
          <tr>
            <td />
            <th scope="colgroup" colSpan={2}>
              {t('newStrategy.results.perYear')}
            </th>
            <th scope="colgroup" colSpan={2}>
              {t('newStrategy.results.worstYear')}
            </th>
            {hasAlpha && <td />}
            {hasStory && <td />}
          </tr>
          <tr>
            <th scope="col">{t('newStrategy.results.period')}</th>
            {pair}
            {pair}
            {hasAlpha && (
              <th scope="col" className={styles.alphaCol}>
                {t('newStrategy.results.alpha')}
                <InfoTip
                  label={t('newStrategy.results.alphaTip.label')}
                  text={t('newStrategy.results.alphaTip.text')}
                />
              </th>
            )}
            {hasStory && (
              <th scope="col" className={styles.storyCol}>
                {t('newStrategy.results.story')}
              </th>
            )}
          </tr>
        </thead>
        <tbody>
          {activePeriods.map((p) => (
            <tr key={p.period}>
              <th scope="row">{p.period}</th>
              {p.templatePerYear === null ? (
                <td colSpan={dataCols} className={styles.emptyPeriod}>
                  {p.story}
                </td>
              ) : (
                <>
                  <td>
                    <Pct value={p.templatePerYear} digits={1} />
                  </td>
                  <td>
                    <Pct value={p.indexPerYear ?? 0} digits={1} plain />
                  </td>
                  <td>
                    <Pct value={p.worstYear ?? 0} lessBad={(p.worstYear ?? 0) > (p.indexWorstYear ?? 0)} />
                  </td>
                  <td>
                    <Pct value={p.indexWorstYear ?? 0} lessBad={(p.indexWorstYear ?? 0) > (p.worstYear ?? 0)} />
                  </td>
                  {hasAlpha && (
                    <td>
                      <Pct value={p.alpha ?? 0} digits={1} />
                    </td>
                  )}
                  {hasStory && <td className={styles.storyCell}>{p.story}</td>}
                </>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )

  if (!benchmarks?.length || !activeBenchmark) return table

  return (
    <div className={styles.periodBlock}>
      <Tabs
        id={tabsId}
        label={t('newStrategy.results.indexTabs')}
        items={benchmarks.map((b) => ({ key: b.key, label: b.name }))}
        selected={activeBenchmark.key}
        onSelect={setActiveKey}
        sharedPanel
      />
      {table}
    </div>
  )
}
