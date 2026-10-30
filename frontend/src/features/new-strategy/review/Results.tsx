import type { PeriodResult } from '../../../api'
import { InfoTip } from '../../../components/InfoTip'
import { useT } from '../../../i18n/I18n'
import { signedPct } from '../../../lib/format'
import styles from './Results.module.css'

/** A result in percent; the sign is always written, colour only repeats it. */
function Pct({ value, plain }: { value: number; plain?: boolean }) {
  return (
    <span className={styles.pct} data-sign={plain ? undefined : value < 0 ? 'neg' : 'pos'}>
      {signedPct(value)}
    </span>
  )
}

/** The three figures of the Tested box: this template, the S&P 500 fund, the worst year. */
export function ResultFigures({ result }: { result: PeriodResult }) {
  const t = useT()
  const figures = [
    { label: t('newStrategy.results.thisTemplate'), value: result.template },
    {
      label: t('newStrategy.results.indexFund'),
      value: result.index,
      plain: true,
      tip: <InfoTip label={t('common.indexTip.label')} text={t('common.indexTip.text')} />,
    },
    { label: t('newStrategy.results.worstYear'), value: result.worstYear },
  ]
  return (
    <dl className={styles.figures}>
      {figures.map((f) => (
        <div key={f.label}>
          <dt>
            {f.label}
            {f.tip}
          </dt>
          <dd>
            <Pct value={f.value} plain={f.plain} />
          </dd>
        </div>
      ))}
    </dl>
  )
}

/** "By period" table on How we tested. */
export function PeriodTable({ periods }: { periods: PeriodResult[] }) {
  const t = useT()
  return (
    <table className={`well ${styles.periods}`}>
      <thead>
        <tr>
          <th scope="col">{t('newStrategy.results.period')}</th>
          <th scope="col">{t('newStrategy.results.template')}</th>
          <th scope="col">{t('newStrategy.results.index')}</th>
          <th scope="col">{t('newStrategy.results.worstYr')}</th>
        </tr>
      </thead>
      <tbody>
        {periods.map((p) => (
          <tr key={p.period}>
            <th scope="row">{p.period}</th>
            <td>
              <Pct value={p.template} />
            </td>
            <td>
              <Pct value={p.index} plain />
            </td>
            <td>
              <Pct value={p.worstYear} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
