import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, InvalidValueError, type AllLine, type TemplateDetail } from '../../api'
import { Button } from '../../components/Button'
import { InfoTip } from '../../components/InfoTip'
import { Section } from '../../components/Section'
import { useT } from '../../i18n/I18n'
import { signedPctOrDash } from '../../lib/format'
import { testedValues, type SettingValues } from '../new-strategy/settings'
import { backtestPath } from './paths'
import styles from './BacktestPanel.module.css'

type Column = 'default' | 'saved' | 'current'

interface Ran {
  /** The values each column ran with, to tell when the screen's settings changed since. */
  current: SettingValues
  lines: Partial<Record<Column, { strategy: AllLine; index: AllLine }>>
}

const same = (a: SettingValues, b: SettingValues) => Object.keys(a).every((k) => a[k] === b[k]) && Object.keys(b).every((k) => a[k] === b[k])
const pct = (n: number | null) => signedPctOrDash(n === null ? null : Math.round(n * 1000) / 10)

/**
 * The backtest preview at the bottom of Advanced settings (docs/backtest.md, *Screens* C; owner only): the average of the periods
 * on the test defaults, for the template's defaults, the saved settings (a running strategy whose settings differ) and the settings
 * on screen, next to the S&P 500, monthly. *Rerun tests* runs them; the page has the rest.
 */
export function BacktestPanel({ template, values, saved, strategyId, from }: {
  template: TemplateDetail
  values: SettingValues
  /** A running strategy's saved settings; none for a new strategy. */
  saved?: SettingValues
  strategyId?: string
  /** The screen the Backtest page goes back to. */
  from: string
}) {
  const t = useT()
  const [ran, setRan] = useState<Ran | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const tested = testedValues(template)
  const columns: Column[] = [
    'default',
    ...(saved && !same(saved, tested) ? (['saved'] as const) : []),
    ...(!same(values, tested) && !(saved && same(values, saved)) ? (['current'] as const) : []),
  ]
  const settingsOf = (column: Column) => (column === 'saved' ? saved! : column === 'current' ? values : undefined)

  const rerun = async () => {
    setBusy(true)
    setError(null)
    try {
      const lines: Ran['lines'] = {}
      for (const column of columns) {
        const run = await api.runBacktest({ templateKey: template.key, settings: settingsOf(column) })
        lines[column] = { strategy: run.result.strategy, index: run.result.index }
      }
      setRan({ current: values, lines })
    } catch (e) {
      setError(e instanceof InvalidValueError ? e.message : t('backtest.panel.failed'))
    } finally {
      setBusy(false)
    }
  }

  const shown = columns.filter((c) => ran?.lines[c])
  const index = shown.length > 0 ? ran!.lines[shown[0]]!.index : null
  const head = { default: t('backtest.panel.default'), saved: t('backtest.panel.saved'), current: t('backtest.panel.current') }
  const rows: { label: string; tip?: string; cell: (l: AllLine) => string }[] = [
    { label: t('backtest.panel.rate'), cell: (l) => pct(l.rate) },
    { label: t('backtest.panel.worst'), cell: (l) => (l.worst ? pct(l.worst.return) : '–') },
    { label: t('backtest.panel.atWork'), tip: t('backtest.panel.atWorkTip'), cell: (l) => `${Math.round(l.budgetAtWork * 100)}%` },
    { label: t('backtest.panel.trades'), cell: (l) => String(l.trades) },
  ]

  return (
    <Section title={t('newStrategy.advanced.backtest')} aside={
      <Button variant="outline" onClick={() => void rerun()} disabled={busy}>
        {busy ? t('backtest.panel.running') : t('backtest.panel.rerun')}
      </Button>
    }>
      <div className={styles.panel} data-busy={busy || undefined}>
        <p className={styles.scope}>{t('backtest.panel.scope')}</p>
        {ran && !same(ran.current, values) && <p className={styles.stale}>{t('backtest.panel.changed')}</p>}
        {error && <p role="alert" className={styles.error}>{error}</p>}
        {!ran && !busy && <p className={styles.scope}>{columns.length === 1 ? t('backtest.panel.onDefaults') : t('backtest.panel.first')}</p>}
        {shown.length > 0 && (
          <table className={styles.table}>
            {shown.length > 1 && (
              <thead>
                <tr>
                  <td />
                  {shown.map((c) => (
                    <th key={c} scope="col">
                      {head[c]}
                    </th>
                  ))}
                </tr>
              </thead>
            )}
            <tbody>
              {rows.map((r) => (
                <tr key={r.label}>
                  <th scope="row" className={styles.tipCell}>
                    {r.label}
                    {r.tip && <InfoTip label={r.label} text={r.tip} />}
                  </th>
                  {shown.map((c) => (
                    <td key={c}>{r.cell(ran!.lines[c]!.strategy)}</td>
                  ))}
                </tr>
              ))}
              {index && (
                <tr>
                  <th scope="row">{t('backtest.panel.index')}</th>
                  <td colSpan={shown.length}>
                    {pct(index.rate)} {shown.length > 1 && <span className={styles.same}>{t('backtest.panel.sameForAll')}</span>}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        )}
        <p className={styles.scope}>{t('backtest.panel.irr')}</p>
        <Link
          className={styles.open}
          to={backtestPath(strategyId ? { strategy: strategyId, from } : { template: template.key, from })}
          state={{ settings: values }}
        >
          {t('backtest.panel.open')}
        </Link>
      </div>
    </Section>
  )
}
