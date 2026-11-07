import { useId, useState, type KeyboardEvent } from 'react'
import type { BenchmarkBacktest, PeriodResult } from '../../../api'
import { InfoTip } from '../../../components/InfoTip'
import { useT } from '../../../i18n/I18n'
import { signedPct } from '../../../lib/format'
import styles from './Results.module.css'

/** A result in percent; the sign is always written, colour only repeats it. */
function Pct({ value, plain, digits }: { value: number; plain?: boolean; digits?: number }) {
  return (
    <span className={styles.pct} data-sign={plain ? undefined : value < 0 ? 'neg' : 'pos'}>
      {signedPct(value, digits)}
    </span>
  )
}

/** The Tested box's figures: the template next to the S&P 500 fund, a year on average and in the worst year. */
export function ResultFigures({ result }: { result: PeriodResult }) {
  const t = useT()
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
            <Pct value={result.worstYear ?? 0} />
          </td>
          <td>
            <Pct value={result.indexWorstYear ?? 0} />
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

  const onTabKeyDown = (e: KeyboardEvent<HTMLButtonElement>, index: number) => {
    if (!benchmarks || benchmarks.length === 0) return
    let next = -1
    if (e.key === 'ArrowRight') next = (index + 1) % benchmarks.length
    else if (e.key === 'ArrowLeft') next = (index - 1 + benchmarks.length) % benchmarks.length
    else if (e.key === 'Home') next = 0
    else if (e.key === 'End') next = benchmarks.length - 1
    if (next >= 0) {
      e.preventDefault()
      const target = benchmarks[next]
      setActiveKey(target.key)
      document.getElementById(`${tabsId}-tab-${target.key}`)?.focus()
    }
  }

  const pair = (
    <>
      <th scope="col">{t('newStrategy.results.template')}</th>
      <th scope="col">{indexLabel}</th>
    </>
  )

  const table = (
    <div className={`well ${styles.tableWrap}`}>
      <table
        className={styles.periods}
        id={activeBenchmark ? `${tabsId}-panel` : undefined}
        role={activeBenchmark ? 'tabpanel' : undefined}
        aria-labelledby={activeBenchmark ? `${tabsId}-tab-${activeBenchmark.key}` : undefined}
      >
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
                    <Pct value={p.worstYear ?? 0} />
                  </td>
                  <td>
                    <Pct value={p.indexWorstYear ?? 0} />
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
      <div className={styles.indexTabs} role="tablist" aria-label={t('newStrategy.results.indexTabs')}>
        {benchmarks.map((b, i) => {
          const selected = b.key === activeBenchmark.key
          return (
            <button
              key={b.key}
              id={`${tabsId}-tab-${b.key}`}
              type="button"
              role="tab"
              aria-selected={selected}
              aria-controls={`${tabsId}-panel`}
              tabIndex={selected ? 0 : -1}
              className={`${styles.indexTab} ${selected ? styles.indexTabActive : ''}`}
              onClick={() => setActiveKey(b.key)}
              onKeyDown={(e) => onTabKeyDown(e, i)}
            >
              {b.name}
            </button>
          )
        })}
      </div>
      {table}
    </div>
  )
}
