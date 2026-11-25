import { useCallback, useEffect, useId, useMemo, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import {
  api,
  InvalidValueError,
  type BacktestRequest,
  type BacktestRun,
  type BacktestTrade,
  type StrategyDetail,
  type TemplateDetail,
  type TemplateKey,
  type TestStocks,
} from '../../api'
import { Button, ButtonRow } from '../../components/Button'
import { DataTable, type DataTableColumn } from '../../components/DataTable'
import { SegmentedField, TextField } from '../../components/Field'
import { InfoTip } from '../../components/InfoTip'
import { Loaded } from '../../components/Loaded'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Tabs } from '../../components/Tabs'
import { tabPanel } from '../../components/tabPanel'
import { exact, formatShares, localDateTime, money, signedPctOrDash } from '../../lib/format'
import { chartLines } from '../../lib/sparkline'
import type { Loadable } from '../../lib/useLoadable'
import { advancedPath } from '../new-strategy/paths'
import { changedSettings, settingValues } from '../new-strategy/settings'
import { useTemplate } from '../new-strategy/useTemplate'
import { strategySettingsPath } from '../dashboard/paths'
import { useStrategy } from '../strategy/useStrategy'
import { ALL, chartSeries, gaps, lineRows, tradesOf, type LineRow } from './backtestView'
import type { BacktestState } from './paths'
import styles from './BacktestPage.module.css'

/** The baselines' names and info tips (docs/backtest.md, *Screens*, B). */
const LINES: Record<LineRow['key'], { name: string; tip?: string }> = {
  strategy: { name: 'Your strategy' },
  index: {
    name: '1 S&P 500, monthly',
    tip: "Buys an S&P 500 fund with each month's money until the budget is used. Does the strategy beat the market?",
  },
  sameDays: {
    name: '2 Same days, same list',
    tip: 'Each time the strategy buys, the same amount goes into all the stocks in the list that day, split evenly. Does the strategy choose the right stocks?',
  },
  sameStocks: {
    name: '3 Same stocks, monthly',
    tip: "Buys all the stocks in the list with each month's money, split evenly. Does the timing matter? Compare it with 2: same stocks, other days.",
  },
}

const AT_WORK_TIP =
  'How much of the budget was invested, on average over the period\'s days. All of it spent on day one is 100%; all of it spent 2 years into a 4-year period is 50%.'

const GAP_TEXT = { market: 'vs 1  the market', choice: 'vs 2  the choice of stocks', timing: '2 vs 3  the timing' } as const

const pct = (n: number | null) => signedPctOrDash(n === null ? null : Math.round(n * 10) / 10)

/**
 * The Backtest page (/backtest, owner only; docs/backtest.md, *Screens* A and B): the test's settings, then the results by period
 * with the three baselines. Opened from a template's review, a strategy, its settings or a stock, each preset by the URL.
 */
export function BacktestPage() {
  const [search] = useSearchParams()
  const strategyId = search.get('strategy')
  return strategyId ? <ForStrategy id={strategyId} /> : <ForTemplate />
}

function ForStrategy({ id }: { id: string }) {
  const strategy = useStrategy(id)
  const template = useTemplate(strategy.status === 'ready' ? strategy.data.templateKey : undefined)
  return <Page template={template} strategy={strategy.status === 'ready' ? strategy.data : null} loading={strategy} />
}

function ForTemplate() {
  const [search] = useSearchParams()
  const template = useTemplate((search.get('template') ?? undefined) as TemplateKey | undefined)
  return <Page template={template} strategy={null} loading={template} />
}

function Page({ template, strategy, loading }: { template: Loadable<TemplateDetail>; strategy: StrategyDetail | null; loading: Loadable<unknown> }) {
  const [search] = useSearchParams()
  const up = search.get('from') ?? '/'
  return (
    <Screen top={<TopBar up={up} step="Backtest" />}>
      <Loaded state={template.status === 'ready' ? template : loading} what="the rules" english>
        {() => template.status === 'ready' && <Backtest template={template.data} strategy={strategy} />}
      </Loaded>
    </Screen>
  )
}

/** The test the page opens with: the entry point's rules, stocks and money; the rest the test defaults. */
function preset(template: TemplateDetail, strategy: StrategyDetail | null, stock: string | null, settings: Record<string, number> | undefined): BacktestRequest {
  const request: BacktestRequest = { templateKey: template.key }
  const params = settings ?? strategy?.settings ?? undefined
  if (params && Object.keys(params).length > 0) request.settings = params
  if (strategy) {
    // A strategy's test uses its own budget, with no monthly limit until strategies have one (ROADMAP item 57).
    request.budget = strategy.budget.amount
    request.monthlyLimit = strategy.budget.amount
    if (strategy.budget.step !== null) request.amountPerTrade = strategy.budget.step
    if (strategy.budget.maxSteps !== null) request.maxTrades = strategy.budget.maxSteps
    request.currency = strategy.currency
  }
  if (stock) {
    request.stocks = 'stock'
    request.symbols = [stock]
    delete request.budget
  }
  return request
}

function Backtest({ template, strategy }: { template: TemplateDetail; strategy: StrategyDetail | null }) {
  const [search] = useSearchParams()
  const location = useLocation()
  const state = location.state as BacktestState | null
  const initial = useMemo(
    () => preset(template, strategy, search.get('stock'), state?.settings),
    // The preset is the page's opening test; later edits live in the form.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  )
  const [request, setRequest] = useState<BacktestRequest>(initial)
  const [run, setRun] = useState<BacktestRun | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const start = useCallback(async (test: BacktestRequest) => {
    setBusy(true)
    setError(null)
    try {
      setRun(await api.runBacktest(test))
    } catch (e) {
      setError(e instanceof InvalidValueError ? e.message : 'The backtest failed. Try again.')
    } finally {
      setBusy(false)
    }
  }, [])

  useEffect(() => void start(initial), [initial, start])

  const changed = Object.keys(changedSettings(template, settingValues(template, request.settings ?? {}))).length
  const rulesFrom = state?.settings ? 'the settings on screen' : strategy ? 'your saved settings' : 'the tested defaults'
  const editRules = strategy ? strategySettingsPath(strategy.id) : advancedPath(template.key)
  return (
    <>
      <PageHeader title="Backtest" lead={strategy ? `${template.name} · ${strategy.name}` : template.name} />
      <p className={styles.rules}>
        Rules: {rulesFrom}
        {changed > 0 && ` · ${changed} changed`}
        <Link to={editRules} className={styles.editRules}>
          Edit rules
        </Link>
      </p>
      <TestSettings
        template={template}
        strategy={strategy}
        request={request}
        run={run}
        busy={busy}
        onChange={setRequest}
        onRun={() => void start(request)}
        onReset={() => setRequest(preset(template, strategy, null, state?.settings))}
      />
      {error && (
        <p className={styles.error} role="alert">
          {error}
        </p>
      )}
      {run && <Results run={run} busy={busy} />}
      {!run && busy && <p className={styles.busy}>Running the backtest…</p>}
    </>
  )
}

const num = (s: string) => (s.trim() === '' ? undefined : Number(s.replace(',', '.')))

function TestSettings({ template, strategy, request, run, busy, onChange, onRun, onReset }: {
  template: TemplateDetail
  strategy: StrategyDetail | null
  request: BacktestRequest
  run: BacktestRun | null
  busy: boolean
  onChange: (r: BacktestRequest) => void
  onRun: () => void
  onReset: () => void
}) {
  const [editing, setEditing] = useState(false)
  const fund = template.investsIn !== 'stockList'
  const setup = run?.setup
  const currency = (setup?.currency ?? request.currency ?? 'EUR') as 'EUR' | 'USD' | 'GBP'
  const field = (value: number | undefined) => (value === undefined ? '' : String(value))
  const set = (patch: Partial<BacktestRequest>) => onChange({ ...request, ...patch })

  const stocksText = !setup
    ? '…'
    : fund
      ? `${setup.index}, one fund`
      : setup.stocks === 'draft'
        ? `Test draft · ${run.pools[0]?.stocks.length ?? 60} from the S&P 500`
        : setup.stocks === 'stock'
          ? `One stock · ${setup.symbols[0]}`
          : `This strategy's list · ${setup.symbols.length}`

  return (
    <Section title="Test settings" aside={!editing && <Button variant="outline" onClick={() => setEditing(true)}>Edit</Button>}>
      {!editing && setup && run && (
        <dl className={`well ${styles.summary}`}>
          <dt>Stocks</dt>
          <dd>{stocksText}</dd>
          <dt>Years</dt>
          <dd>
            {setup.years === setup.periodYears ? `The latest ${setup.years}` : `${setup.years}, in ${setup.periodYears}-year periods`}
          </dd>
          <dt>Money</dt>
          <dd>
            {money(setup.budget, currency)}
            {setup.amountPerTrade !== null ? ` · ${money(setup.amountPerTrade, currency)} per trade · ${setup.maxTrades} a stock` : ' · all in'}
            {setup.monthlyLimit < setup.budget && ` · at most ${money(setup.monthlyLimit, currency)} a month`}
          </dd>
          <dt>Costs</dt>
          <dd>
            {setup.feePct}% fee · {setup.spreadPct}% spread a trade
          </dd>
          <dt>Data</dt>
          <dd>
            {run.dataFrom.slice(0, 7)} to {run.dataTo.slice(0, 7)} · snapshot {run.snapshot}
          </dd>
        </dl>
      )}
      {editing && (
        <form
          className={styles.form}
          onSubmit={(e) => {
            e.preventDefault()
            onRun()
          }}
        >
          {!fund && (
            <SegmentedField<TestStocks>
              label="Stocks"
              value={request.stocks ?? 'draft'}
              onChange={(stocks) =>
                set({
                  stocks,
                  symbols: stocks === 'list' ? strategy?.stocks.map((s) => s.symbol) : stocks === 'stock' ? request.symbols?.slice(0, 1) : undefined,
                  years: stocks === 'draft' ? undefined : request.years,
                })
              }
              options={[
                { value: 'draft', label: 'Test draft' },
                ...(strategy ? [{ value: 'list' as const, label: 'This list' }] : []),
                { value: 'stock', label: 'One stock' },
              ]}
            />
          )}
          {!fund && request.stocks === 'stock' && (
            <TextField label="Stock" value={request.symbols?.[0] ?? ''} onChange={(v) => set({ symbols: [v.toUpperCase()] })} autoCapitalize="characters" />
          )}
          {fund && <TextField label="Index fund" value={request.index ?? 'SPY'} onChange={(v) => set({ index: v.toUpperCase() })} autoCapitalize="characters" />}
          <div className={styles.pair}>
            <TextField label="Years" type="decimal" value={field(request.years ?? setup?.years)} onChange={(v) => set({ years: num(v) })} />
            <TextField label="Years a period" type="decimal" value={field(request.periodYears ?? setup?.periodYears)} onChange={(v) => set({ periodYears: num(v) })} />
          </div>
          <div className={styles.pair}>
            <TextField label="Budget" type="decimal" value={field(request.budget ?? setup?.budget)} onChange={(v) => set({ budget: num(v) })} />
            {(setup?.amountPerTrade ?? request.amountPerTrade) != null && (
              <TextField label="Per trade" type="decimal" value={field(request.amountPerTrade ?? setup?.amountPerTrade ?? undefined)} onChange={(v) => set({ amountPerTrade: num(v) })} />
            )}
          </div>
          <div className={styles.pair}>
            {(setup?.amountPerTrade ?? request.amountPerTrade) != null && (
              <TextField label="Trades a stock" type="decimal" value={field(request.maxTrades ?? setup?.maxTrades)} onChange={(v) => set({ maxTrades: num(v) })} />
            )}
            <TextField
              label="Each month at most"
              type="decimal"
              value={field(request.monthlyLimit ?? setup?.monthlyLimit)}
              onChange={(v) => set({ monthlyLimit: num(v) })}
              hint="Unspent room carries over."
            />
          </div>
          <div className={styles.pair}>
            <TextField label="Fee %" type="decimal" value={field(request.feePct ?? setup?.feePct)} onChange={(v) => set({ feePct: num(v) })} />
            <TextField label="Spread %" type="decimal" value={field(request.spreadPct ?? setup?.spreadPct)} onChange={(v) => set({ spreadPct: num(v) })} />
          </div>
          <ButtonRow>
            <Button variant="outline" onClick={onReset}>
              Reset to test defaults
            </Button>
            <Button variant="primary" type="submit" disabled={busy}>
              {busy ? 'Running…' : 'Run backtest'}
            </Button>
          </ButtonRow>
        </form>
      )}
    </Section>
  )
}

function Results({ run, busy }: { run: BacktestRun; busy: boolean }) {
  const id = useId()
  const periods = run.result.periods
  const [tab, setTab] = useState(periods.length > 1 ? ALL : periods[0].period.label)
  const [showTrades, setShowTrades] = useState(false)
  const rows = lineRows(run, tab)
  const period = periods.find((p) => p.period.label === tab)
  const trades = tradesOf(run, tab)
  const currency = run.setup.currency as 'EUR' | 'USD' | 'GBP'
  const stocks = period ? period.stocks : periods[0].stocks

  return (
    <Section title="Results" aside={<span className={styles.ran}>ran {localDateTime(run.ranAt)}</span>}>
      <div data-busy={busy || undefined} className={styles.results}>
        {periods.length > 1 && (
          <Tabs
            id={id}
            label="Period"
            sharedPanel
            selected={tab}
            onSelect={setTab}
            items={[...periods.map((p) => ({ key: p.period.label, label: p.period.label })), { key: ALL, label: 'All' }]}
          />
        )}
        <div {...tabPanel(id, tab, true)}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th scope="col">
                  <span className="visually-hidden">Line</span>
                </th>
                <th scope="col">A year, average</th>
                <th scope="col">Worst year</th>
                <th scope="col" className={styles.tipCell}>
                  Budget at work <InfoTip label="Budget at work" text={AT_WORK_TIP} />
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.key} data-strategy={r.key === 'strategy' || undefined}>
                  <th scope="row" className={styles.tipCell}>
                    {lineName(r.key, run)}
                    {LINES[r.key].tip && <InfoTip label={LINES[r.key].name} text={LINES[r.key].tip!} />}
                  </th>
                  <td>{pct(r.rate)}</td>
                  <td>{r.worst ? pct(r.worst.return * 100) : '–'}</td>
                  <td>{Math.round(r.atWork)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className={styles.caption}>
            {rows[0].trades} trades · {run.setup.stocks === 'draft' ? `test draft: ${stocks} stocks` : `${stocks} ${stocks === 1 ? 'stock' : 'stocks'}`}
            {tab === ALL && ` · average of ${periods.length} periods, each a fresh test`}
            {period?.period.partial && ' · runs to the last close'}
            {period && period.unpaid > 0 && ` · ${period.unpaid} signals the money couldn't pay`}
            {period && period.satOut.length > 0 && ` · sat out: ${period.satOut.join(', ')}`}
          </p>
          <p className={styles.caption}>Money-weighted returns (IRR) on the money invested: idle cash is left out.</p>
          {tab !== ALL && <ValueChart run={run} tab={tab} />}
          {gaps(rows).length > 0 && (
            <div className={styles.gaps}>
              <h3 className="ui-strong">What the gaps say</h3>
              <dl>
                {gaps(rows).map((g) => (
                  <div key={g.key}>
                    <dt>{GAP_TEXT[g.key]}</dt>
                    <dd>{pct(g.points).replace('%', '')} a year</dd>
                  </div>
                ))}
              </dl>
            </div>
          )}
          {trades.length > 0 && (
            <TextToggle open={showTrades} onToggle={() => setShowTrades((s) => !s)}>
              {showTrades ? 'Hide the trades' : `See the ${trades.length} trades`}
            </TextToggle>
          )}
          {showTrades && <TradesTable trades={trades} currency={currency} />}
        </div>
      </div>
    </Section>
  )
}

/** A baseline's name: for one stock or one fund, baseline 3 is that stock, monthly. */
function lineName(key: LineRow['key'], run: BacktestRun) {
  if (key === 'sameStocks' && run.setup.stocks !== 'draft' && run.setup.symbols.length === 1) return `3 ${run.setup.symbols[0]}, monthly`
  return LINES[key].name
}

function TextToggle({ open, onToggle, children }: { open: boolean; onToggle: () => void; children: string }) {
  return (
    <button type="button" className={`ui-strong ${styles.toggle}`} aria-expanded={open} onClick={onToggle}>
      {children}
    </button>
  )
}

const WIDTH = 326
const HEIGHT = 80

/** The value of the money invested, as a gain in %: the strategy and baseline 1. */
function ValueChart({ run, tab }: { run: BacktestRun; tab: string }) {
  const series = chartSeries(run, tab)
  const [strategy, index] = chartLines([series.strategy, series.index], WIDTH, HEIGHT)
  if (!strategy) return null
  return (
    <figure className={styles.chart}>
      <svg width="100%" height={HEIGHT} viewBox={`0 0 ${WIDTH} ${HEIGHT}`} preserveAspectRatio="none" aria-hidden="true">
        {index && <polyline points={index} className={styles.lineIndex} vectorEffect="non-scaling-stroke" />}
        <polyline points={strategy} className={styles.lineStrategy} vectorEffect="non-scaling-stroke" />
      </svg>
      <figcaption className={styles.legend}>
        <span>
          <span className={styles.keyStrategy}>━</span> strategy
        </span>
        <span>
          <span className={styles.keyIndex}>┅</span> S&amp;P 500, monthly
        </span>
        <span className="visually-hidden">Gain on the money invested, at each month's end.</span>
      </figcaption>
    </figure>
  )
}

function TradesTable({ trades, currency }: { trades: BacktestTrade[]; currency: 'EUR' | 'USD' | 'GBP' }) {
  const columns: DataTableColumn<BacktestTrade>[] = [
    { id: 'date', header: 'Date', accessorFn: (t) => t.date, mono: true },
    { id: 'stock', header: 'Stock', accessorFn: (t) => t.symbol, mono: true },
    { id: 'side', header: 'Side', accessorFn: (t) => (t.side === 'buy' ? 'Buy' : 'Sell') },
    { id: 'trade', header: 'Trade', accessorFn: (t) => (t.stepNo === null ? '' : `${t.stepNo} of ${t.maxSteps}`), mono: true },
    { id: 'price', header: 'Price', accessorFn: (t) => t.price, cell: (t) => formatShares(t.price), mono: true, align: 'right' },
    { id: 'amount', header: 'Amount', accessorFn: (t) => t.amount, cell: (t) => exact(t.amount, currency), mono: true, align: 'right' },
    { id: 'costs', header: 'Costs', accessorFn: (t) => t.costs, cell: (t) => exact(t.costs, currency), mono: true, align: 'right' },
  ]
  return (
    <DataTable
      label="Trades"
      data={trades}
      getRowId={(t) => `${t.date}-${t.symbol}-${t.side}-${t.stepNo ?? 'x'}-${t.amount}`}
      columns={columns}
      filters={[{ columnId: 'side', label: 'Side', allLabel: 'Buys and sells', options: [{ value: 'Buy', label: 'Buys' }, { value: 'Sell', label: 'Sells' }] }]}
      searchLabel="Search the trades"
      initialSort={{ id: 'date' }}
      pageSize={20}
    />
  )
}
