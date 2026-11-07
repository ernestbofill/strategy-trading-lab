import type { PeriodResult } from '../../../api'
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
            <Pct value={result.templatePerYear} digits={1} />
          </td>
          <td>
            <Pct value={result.indexPerYear} digits={1} plain />
          </td>
        </tr>
        <tr>
          <th scope="row">{t('newStrategy.results.worstYear')}</th>
          <td>
            <Pct value={result.worstYear} />
          </td>
          <td>
            <Pct value={result.indexWorstYear} />
          </td>
        </tr>
      </tbody>
    </table>
  )
}

/** "By period" table on How we tested: a year on average and the worst year, the template next to the S&P 500. */
export function PeriodTable({ periods }: { periods: PeriodResult[] }) {
  const t = useT()
  const pair = (
    <>
      <th scope="col">{t('newStrategy.results.template')}</th>
      <th scope="col">{t('newStrategy.results.index')}</th>
    </>
  )
  return (
    <table className={`well ${styles.periods}`}>
      <thead>
        <tr>
          <td />
          <th scope="colgroup" colSpan={2}>
            {t('newStrategy.results.perYear')}
          </th>
          <th scope="colgroup" colSpan={2}>
            {t('newStrategy.results.worstYear')}
          </th>
        </tr>
        <tr>
          <th scope="col">{t('newStrategy.results.period')}</th>
          {pair}
          {pair}
        </tr>
      </thead>
      <tbody>
        {periods.map((p) => (
          <tr key={p.period}>
            <th scope="row">{p.period}</th>
            <td>
              <Pct value={p.templatePerYear} digits={1} />
            </td>
            <td>
              <Pct value={p.indexPerYear} digits={1} plain />
            </td>
            <td>
              <Pct value={p.worstYear} />
            </td>
            <td>
              <Pct value={p.indexWorstYear} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
