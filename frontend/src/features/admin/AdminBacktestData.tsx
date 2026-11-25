import { useEffect, useId, useState } from 'react'
import {
  api,
  InvalidValueError,
  UnauthorizedError,
  type BacktestDataStatus,
  type CandidateYear,
  type DrawPeriodReport,
  type DrawReport,
  type MissingMember,
  type MissingReason,
  type PriceWarning,
  type RenamedTicker,
  type SnapshotTicker,
} from '../../api'
import { Button } from '../../components/Button'
import { DataTable, type DataTableColumn } from '../../components/DataTable'
import { Loaded } from '../../components/Loaded'
import { PageHeader } from '../../components/PageHeader'
import { Screen } from '../../components/Screen'
import { Tabs } from '../../components/Tabs'
import { tabPanel } from '../../components/tabPanel'
import { localDateTime, signedPct } from '../../lib/format'
import { reload, useLoadable } from '../../lib/useLoadable'
import { AdminTopBar } from './AdminTopBar'
import { adminCacheKeys } from './cacheKeys'
import styles from './AdminBacktestData.module.css'

const POLL_MS = 3000

const reasonText: Record<MissingReason, string> = {
  noSymbol: 'Another company today',
  noPrices: 'No prices on Yahoo',
  nameDiffers: 'Name differs',
  noSector: 'No sector',
  startsLate: 'Prices start too late',
  noPricesAtStart: 'No prices at the start',
}

const thirdText = { 1: 'Low', 2: 'Middle', 3: 'High' } as const

/** A yearly rate (a fraction) as "+12.3%". */
const rate = (r: number | null) => (r === null ? '–' : signedPct(r * 100, 1))

/** "312 of 709" while a job reports how far it is. */
const progressText = (status: BacktestDataStatus) => {
  const p = status.running?.progress
  return p ? `${p.step}${p.total > 1 ? `: ${p.done} of ${p.total}` : '…'}` : 'Starting…'
}

/**
 * Backtest data (/admin/backtest-data, owner only; docs/backtest.md, *Data*): draws the test draft and takes the monthly snapshot,
 * and shows the quality report to review before any backtest result counts.
 */
export function AdminBacktestData() {
  const state = useLoadable(adminCacheKeys.backtestData, api.getAdminBacktestData)
  const running = state.status === 'ready' && state.data.running !== null

  // While a draw or a snapshot runs, look again every few seconds.
  useEffect(() => {
    if (!running) return
    const timer = setInterval(() => void reload(adminCacheKeys.backtestData), POLL_MS)
    return () => clearInterval(timer)
  }, [running])

  return (
    <Screen top={<AdminTopBar />}>
      <PageHeader title="Backtest data" lead="The test draft, a monthly snapshot of its prices, and the quality report. Review it before any result counts." />
      <Loaded state={state} what="the backtest data" english>
        {(status) => (
          <>
            <JobBox status={status} />
            {status.draw && <DrawSections draw={status.draw} warnings={status.snapshot?.warnings ?? status.draw.warnings} />}
            {status.snapshot && <SnapshotSection tickers={status.snapshot.tickers} />}
          </>
        )}
      </Loaded>
    </Screen>
  )
}

function JobBox({ status }: { status: BacktestDataStatus }) {
  const [message, setMessage] = useState<string | null>(null)
  const { draw, snapshot, running, last, membership } = status

  const start = async (call: () => Promise<void>) => {
    setMessage(null)
    try {
      await call()
      await reload(adminCacheKeys.backtestData, { fresh: true })
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setMessage(e instanceof InvalidValueError ? e.message : "Couldn't start it. Try again.")
    }
  }

  return (
    <section className={styles.group} aria-labelledby="backtest-job">
      <h2 id="backtest-job" className={styles.groupTitle}>
        Draft and snapshot
      </h2>
      <p className={styles.line}>
        {draw
          ? `Drawn ${localDateTime(draw.drawnAt)} · seed ${draw.seed} · ${draw.size} stocks · membership to ${draw.membershipLastRow} · ${draw.symbols} symbols asked of Yahoo`
          : 'Not drawn yet. A draw asks Yahoo for every member since 2009: about 15 minutes.'}
      </p>
      {snapshot && (
        <p className={styles.line}>
          Snapshot {snapshot.month}, taken {localDateTime(snapshot.takenAt)}: {snapshot.tickers.filter((t) => t.days > 0).length} tickers,{' '}
          {snapshot.tickers.filter((t) => t.problem).length} without prices.
        </p>
      )}
      {membership.changedSinceDraw && (
        <p role="note" className={styles.warning}>
          The membership files changed since this draw (fja to {membership.lastRow}): draw again.
        </p>
      )}
      {!status.storage && (
        <p role="note" className={styles.warning}>
          Object storage isn't configured on this server: nothing can be drawn.
        </p>
      )}
      {running && (
        <p role="status" className={styles.line}>
          {running.kind === 'draw' ? 'Drawing' : 'Taking the snapshot'} since {localDateTime(running.started)}. {progressText(status)}
        </p>
      )}
      {!running && last?.error && (
        <p role="alert" className={styles.error}>
          The last {last.kind} failed: {last.error}
        </p>
      )}
      <div className={styles.actions}>
        <Button variant="outline" disabled={running !== null || !status.storage} onClick={() => start(api.drawAdminBacktest)}>
          {running?.kind === 'draw' ? 'Drawing…' : draw ? 'Draw again' : 'Draw the test draft'}
        </Button>
        <Button variant="outline" disabled={running !== null || !draw || !status.storage} onClick={() => start(api.snapshotAdminBacktest)}>
          {running?.kind === 'snapshot' ? 'Taking it…' : "Take this month's snapshot"}
        </Button>
      </div>
      {message && (
        <p role="alert" className={styles.error}>
          {message}
        </p>
      )}
    </section>
  )
}

function DrawSections({ draw, warnings }: { draw: DrawReport; warnings: PriceWarning[] }) {
  const tabsId = useId()
  const [shown, setShown] = useState(draw.periods[0]?.label ?? '')

  return (
    <>
      <section className={styles.group} aria-labelledby="backtest-periods">
        <h2 id="backtest-periods" className={styles.groupTitle}>
          Periods
        </h2>
        <Tabs id={tabsId} label="Period" items={draw.periods.map((p) => ({ key: p.label, label: p.label }))} selected={shown} onSelect={setShown} />
        {draw.periods.map((p) => (
          <div key={p.label} {...tabPanel(tabsId, p.label)} hidden={p.label !== shown}>
            <PeriodPanel period={p} />
          </div>
        ))}
      </section>
      <section className={styles.group} aria-labelledby="backtest-candidates">
        <h2 id="backtest-candidates" className={styles.groupTitle}>
          Candidates per template and year
        </h2>
        <p className={styles.muted}>
          Different stocks with a buy signal (needs 4), stocks that were candidates, and buy signals, before the budget's limits.
        </p>
        <CandidatesTable rows={draw.candidates} />
      </section>
      <section className={styles.group} aria-labelledby="backtest-missing">
        <h2 id="backtest-missing" className={styles.groupTitle}>
          Members that can't be drawn ({draw.missing.length})
        </h2>
        <DataTable
          label="Members that can't be drawn"
          data={draw.missing}
          getRowId={(m) => `${m.ticker}-${m.reason}`}
          columns={missingColumns}
          filters={[
            {
              columnId: 'reason',
              label: 'Reason',
              allLabel: 'All reasons',
              options: Object.entries(reasonText).map(([value, label]) => ({ value, label })),
            },
          ]}
          searchPlaceholder="Search tickers…"
          initialSort={{ id: 'ticker', desc: false }}
          emptyMessage="Every member can be drawn."
        />
      </section>
      <section className={styles.group} aria-labelledby="backtest-renamed">
        <h2 id="backtest-renamed" className={styles.groupTitle}>
          Renamed tickers ({draw.renamed.length})
        </h2>
        <p className={styles.muted}>From the symbol map. Without a Yahoo symbol, the ticker is another company today.</p>
        <DataTable
          label="Renamed tickers"
          data={draw.renamed}
          getRowId={(r) => `${r.ticker}-${r.symbol ?? ''}`}
          columns={renamedColumns}
          searchPlaceholder="Search tickers…"
          initialSort={{ id: 'ticker', desc: false }}
          emptyMessage="No renamed tickers."
        />
      </section>
      <section className={styles.group} aria-labelledby="backtest-warnings">
        <h2 id="backtest-warnings" className={styles.groupTitle}>
          Price warnings ({warnings.length})
        </h2>
        <p className={styles.muted}>Gaps over 5 trading days, one-day moves over 40% and prices that stop, in the snapshot's tickers.</p>
        <DataTable
          label="Price warnings"
          data={warnings}
          getRowId={(w) => `${w.symbol}-${w.kind}-${w.date}`}
          columns={warningColumns}
          filters={[
            {
              columnId: 'kind',
              label: 'Kind',
              allLabel: 'All kinds',
              options: Object.entries(warningText).map(([value, label]) => ({ value, label })),
            },
          ]}
          searchPlaceholder="Search tickers…"
          initialSort={{ id: 'symbol', desc: false }}
          emptyMessage="No warnings."
        />
      </section>
    </>
  )
}

function PeriodPanel({ period: p }: { period: DrawPeriodReport }) {
  const r = p.representative
  return (
    <div className={styles.period}>
      <p className={styles.line}>
        {p.start} to {p.end}
        {p.partial ? ' (to the last close)' : ''} · {p.members} members · {p.eligible} can be drawn · {p.members - p.eligible} can't
      </p>
      <p className={r.passed ? styles.line : styles.warning} role={r.passed ? undefined : 'note'}>
        Representative: same stocks, monthly, on the draft {rate(r.draftRate)} a year; on every member that can be drawn {rate(r.allRate)}.{' '}
        {r.passed ? 'Within 2 points.' : 'More than 2 points apart: decide before any result counts.'}
      </p>
      <p className={styles.line}>
        Second list (Wikipedia): {p.secondList.wikipediaMembers} members; {p.secondList.onlyFja.length} only in fja, {p.secondList.onlyWikipedia.length}{' '}
        only in Wikipedia.
      </p>
      {p.secondList.onlyFja.length + p.secondList.onlyWikipedia.length > 0 && (
        <dl className={styles.lists}>
          <dt>Only in fja</dt>
          <dd className={styles.mono}>{p.secondList.onlyFja.join(' ') || '–'}</dd>
          <dt>Only in Wikipedia</dt>
          <dd className={styles.mono}>{p.secondList.onlyWikipedia.join(' ') || '–'}</dd>
        </dl>
      )}
      {p.notes.map((n) => (
        <p key={n} role="note" className={styles.warning}>
          {n}
        </p>
      ))}
      <h3 className={styles.subTitle}>Sectors</h3>
      <div className={styles.scroll}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Sector</th>
              <th scope="col">Can be drawn</th>
              <th scope="col">Seats</th>
            </tr>
          </thead>
          <tbody>
            {p.sectors.map((s) => (
              <tr key={s.sector}>
                <th scope="row">{s.sector}</th>
                <td>{s.eligible}</td>
                <td>{s.seats}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {p.replaced.length > 0 && (
        <>
          <h3 className={styles.subTitle}>
            Kept {p.kept}, replaced {p.replaced.length}
          </h3>
          <ul className={styles.replaced}>
            {p.replaced.map((x) => (
              <li key={x.left.symbol} className={styles.mono}>
                {x.left.symbol} → {x.replacement?.symbol ?? 'none'} <span className={styles.muted}>({x.left.sector}, {thirdText[x.left.third].toLowerCase()})</span>
              </li>
            ))}
          </ul>
        </>
      )}
      <h3 className={styles.subTitle}>The draft ({p.draft.length})</h3>
      <DataTable
        label={`The draft, ${p.label}`}
        data={p.draft}
        getRowId={(s) => s.symbol}
        columns={draftColumns}
        searchPlaceholder="Search stocks…"
        initialSort={{ id: 'sector', desc: false }}
        emptyMessage="No stocks."
      />
    </div>
  )
}

function CandidatesTable({ rows }: { rows: CandidateYear[] }) {
  const templates = [...new Set(rows.map((r) => r.template))]
  const years = [...new Set(rows.map((r) => r.year))].sort()
  const cell = (template: string, year: number) => rows.find((r) => r.template === template && r.year === year)
  return (
    <div className={styles.scroll}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col">Year</th>
            {templates.map((t) => (
              <th key={t} scope="col">
                {t}: signalled · candidates · signals
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {years.map((y) => (
            <tr key={y}>
              <th scope="row">{y}</th>
              {templates.map((t) => {
                const c = cell(t, y)
                return (
                  <td key={t} className={c && !c.passed ? styles.short : undefined}>
                    {c ? `${c.signalled} · ${c.candidates} · ${c.buySignals}` : '–'}
                  </td>
                )
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function SnapshotSection({ tickers }: { tickers: SnapshotTicker[] }) {
  return (
    <section className={styles.group} aria-labelledby="backtest-snapshot">
      <h2 id="backtest-snapshot" className={styles.groupTitle}>
        Snapshot tickers ({tickers.length})
      </h2>
      <DataTable
        label="Snapshot tickers"
        data={tickers}
        getRowId={(t) => t.symbol}
        columns={tickerColumns}
        filters={[{ columnId: 'roles', label: 'Role', allLabel: 'All roles', options: Object.entries(roleText).map(([value, label]) => ({ value, label })) }]}
        searchPlaceholder="Search tickers…"
        initialSort={{ id: 'symbol', desc: false }}
        emptyMessage="No tickers."
      />
    </section>
  )
}

const missingColumns: DataTableColumn<MissingMember>[] = [
  { id: 'ticker', header: 'Ticker', accessorFn: (m) => m.ticker, mono: true },
  { id: 'symbol', header: 'Yahoo', accessorFn: (m) => m.symbol ?? '', cell: (m) => m.symbol ?? '–', mono: true },
  { id: 'periods', header: 'Periods', accessorFn: (m) => m.periods.join(', ') },
  { id: 'reason', header: 'Reason', accessorFn: (m) => m.reason, cell: (m) => reasonText[m.reason] },
  { id: 'detail', header: 'Detail', accessorFn: (m) => m.detail },
]

const renamedColumns: DataTableColumn<RenamedTicker>[] = [
  { id: 'ticker', header: 'Ticker', accessorFn: (r) => r.ticker, mono: true },
  { id: 'symbol', header: 'Yahoo', accessorFn: (r) => r.symbol ?? '', cell: (r) => r.symbol ?? 'none', mono: true },
  { id: 'note', header: 'Why', accessorFn: (r) => r.note },
]

const warningText = { gap: 'Gap', jump: 'Jump', stops: 'Stops' } as const

const warningColumns: DataTableColumn<PriceWarning>[] = [
  { id: 'symbol', header: 'Ticker', accessorFn: (w) => w.symbol, mono: true },
  { id: 'kind', header: 'Kind', accessorFn: (w) => w.kind, cell: (w) => warningText[w.kind] },
  { id: 'date', header: 'Day', accessorFn: (w) => w.date, mono: true },
  { id: 'detail', header: 'Detail', accessorFn: (w) => w.detail },
]

const draftColumns: DataTableColumn<DrawPeriodReport['draft'][number]>[] = [
  { id: 'symbol', header: 'Yahoo', accessorFn: (s) => s.symbol, mono: true },
  { id: 'ticker', header: 'Ticker', accessorFn: (s) => s.ticker, mono: true },
  { id: 'sector', header: 'Sector', accessorFn: (s) => s.sector },
  { id: 'third', header: 'Size', accessorFn: (s) => s.third, cell: (s) => thirdText[s.third] },
]

const roleText = { draft: 'Draft', index: 'Index', starter: 'Starter list' } as const

const tickerColumns: DataTableColumn<SnapshotTicker>[] = [
  { id: 'symbol', header: 'Ticker', accessorFn: (t) => t.symbol, mono: true },
  { id: 'name', header: 'Name', accessorFn: (t) => t.name ?? '', cell: (t) => t.name ?? '–' },
  // One role per row for the filter: a ticker in the draft and a starter list is filtered as the draft.
  { id: 'roles', header: 'Role', accessorFn: (t) => t.roles[0], cell: (t) => t.roles.map((r) => roleText[r]).join(', ') },
  { id: 'first', header: 'From', accessorFn: (t) => t.first ?? '', cell: (t) => t.first ?? '–', mono: true },
  { id: 'last', header: 'To', accessorFn: (t) => t.last ?? '', cell: (t) => t.last ?? '–', mono: true },
  { id: 'days', header: 'Days', accessorFn: (t) => t.days, mono: true, align: 'right' },
  { id: 'problem', header: 'Problem', accessorFn: (t) => t.problem ?? '', cell: (t) => t.problem ?? '–' },
]
