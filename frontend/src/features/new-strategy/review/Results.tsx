import type { PeriodResult } from '../../../api'
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

/** The three figures of the Tested box: this template, the index fund, the worst year. */
export function ResultFigures({ result }: { result: PeriodResult }) {
  const figures = [
    { label: 'This template', value: result.template },
    { label: 'Index fund', value: result.index, plain: true },
    { label: 'Worst year', value: result.worstYear },
  ]
  return (
    <dl className={styles.figures}>
      {figures.map((f) => (
        <div key={f.label}>
          <dt>{f.label}</dt>
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
  return (
    <table className={`well ${styles.periods}`}>
      <thead>
        <tr>
          <th scope="col">Period</th>
          <th scope="col">Template</th>
          <th scope="col">Index</th>
          <th scope="col">Worst yr</th>
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
