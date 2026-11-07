// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type RuleCheck, type StockRow, type StrategyDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { StockPage } from './StockPage'

afterEach(() => {
  cleanup()
  invalidateAll()
  vi.restoreAllMocks()
})

const check = (fields: Partial<RuleCheck> & Pick<RuleCheck, 'key' | 'group' | 'met'>): RuleCheck => ({
  value: null, min: null, max: null, level: null, progress: null, used: null, ...fields,
})

const zs: StockRow = {
  assetId: 'zs', symbol: 'ZS', name: 'Zscaler', status: 'watching', steps: 0, rulesSteps: 0, maxSteps: 5,
  distancePct: 0, signalId: null, signalStep: null, invested: 0, result: null,
  exchange: 'NASDAQ', currency: 'USD', what: 'Cloud security.', checkedOn: '2026-10-30', close: 237.62,
  candidateSince: '2026-09-11',
  checks: [
    check({
      key: 'beaten', group: 'candidate', met: true, value: 12, min: 30, max: 60,
      date: '2026-09-11', subValue: -29.2, level: 335.77, endValue: 42.5, endLevel: 166.86,
    }),
    check({ key: 'buyLine', group: 'buy', met: true, value: 1.6, min: 15, max: 8, level: 233.9, midLevel: 203.39, endLevel: 252.61 }),
    check({ key: 'volume', group: 'buy', met: false, value: 0.84, min: 1.1, progress: 0.76 }),
    check({ key: 'sma200', group: 'next', met: false, value: 42.5, max: 8, level: 166.86, endLevel: 180.21, preview: true }),
  ],
}

const strategy = (stocks: StockRow[]): StrategyDetail => ({
  id: 's1', name: 'Comebacks', templateKey: 'comeback', templateName: 'The comeback', investsIn: 'stockList', mode: 'live',
  status: 'running', endedOn: null, startedOn: '2026-10-13', today: '2026-10-31', currency: 'EUR',
  budget: { amount: 3000, used: 0, step: 100, maxSteps: 5, steps: 0, totalSteps: 30 }, results: null, pastResult: null,
  signals: { did: 0, skipped: 0, missed: 0 }, words: { zone: 'the buy line', notCandidate: 'Not beaten down' },
  stocks, trades: [], signalHistory: [], rates: [],
  checkTexts: {
    beaten: {
      label: 'Beaten down lately',
      detail: 'Candidate since {date}. Now {+subValue}% from the 52-week high ({level}) and {+endValue}% vs the SMA 200 ({endLevel}).',
      past: 'Window closed: last beaten down on {date} ({value} trading days ago; window is {max} days).',
      short: 'not beaten down lately',
      pastShort: 'window closed ({value}d ago, max {max}d)',
    },
    buyLine: {
      label: 'Price vs the buy line',
      detail: '{+value}% vs the buy line ({level}). A step needs 0% to +{max}% (up to {endLevel}).',
      past: 'Past chase limit: {+value}% vs the buy line ({level}); more than +{max}% ({endLevel}) is too late.',
      short: '{+value}% vs the buy line',
      info: 'The SMA 50 ({midLevel}) + {min}%.',
    },
    volume: { label: 'Volume: 5-day vs 50-day average', detail: '{value}× its 50-day average (needs {min}×).', short: 'volume {value}× (needs {min}×)' },
    sma200: {
      label: 'Back above SMA 200',
      detail: '{+value}% vs the SMA 200 ({level}). A step needs 0% to +{max}% (up to {endLevel}).',
      past: 'Past chase limit: {+value}% above the SMA 200 ({level}); limit is +{max}% ({endLevel}).',
      short: 'below the SMA 200',
    },
    gain: { label: 'Proven growth', detail: 'Rose {value}% from its low (needs {min}%).', short: 'rose {value}% (needs {min}%)' },
    stacked: {
      label: 'Averages stacked',
      detail: 'Short {level} · middle {midLevel} · long {endLevel} (needs short > middle > long).',
      short: 'averages not stacked in order',
    },
    zone: {
      label: 'In the discount zone',
      detail: '{value}% below the recent peak. The zone is {min}–{max}% below ({level} to {endLevel}).',
      past: 'Fell past the discount zone: {value}% below the peak; the zone is {min}–{max}% ({level} to {endLevel}).',
      short: '{value}% below its peak (zone {min}–{max}%)',
      pastShort: '{value}% below peak (past {max}% zone)',
    },
  },
})

function open(s: StrategyDetail, assetId = 'zs') {
  vi.spyOn(api, 'getStrategy').mockResolvedValue(s)
  vi.spyOn(api, 'getStockPrices').mockResolvedValue([
    { date: '2026-10-01', close: 220 },
    { date: '2026-10-30', close: 237.62 },
  ])
  render(
    <MemoryRouter initialEntries={[`/strategies/s1/stocks/${assetId}`]}>
      <Routes>
        <Route path="/strategies/:id/stocks/:assetId" element={<StockPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('StockPage', () => {
  it('shows candidate since date, recovery context, visual bars, and a collapsible next-steps preview', async () => {
    open(strategy([zs]))

    expect(await screen.findByRole('heading', { name: 'ZS' })).toBeTruthy()
    expect(screen.getByText('CANDIDATE')).toBeTruthy()
    expect(screen.getByText('Volume 0.84× (needs 1.1×)')).toBeTruthy()
    expect(screen.getByText('$237.62')).toBeTruthy()
    expect(screen.getByText('Close on Oct 30')).toBeTruthy()
    expect(screen.getByText('Candidate since Sep 11')).toBeTruthy()
    // The chart: a candidate's buy area, from the buy line to the chase limit.
    expect(await screen.findByText('$233.90–$252.61')).toBeTruthy()
    expect(screen.getByRole('radio', { name: '30 days' })).toHaveProperty('checked', true)
    expect(screen.getByText('Yes, since Sep 11: the rules watch it for a buy signal.')).toBeTruthy()
    expect(screen.getByText('1 of 2 met. All of them: a buy signal.')).toBeTruthy()
    expect(
      screen.getByText(
        'Candidate since Sep 11. Now −29.2% from the 52-week high ($335.77) and +42.5% vs the SMA 200 ($166.86).',
      ),
    ).toBeTruthy()
    expect(screen.queryByText(/Day \d+ of|^\+12%$/)).toBeNull()
    expect(screen.getByText('+1.6% vs the buy line ($233.90). A step needs 0% to +8% (up to $252.61).')).toBeTruthy()
    expect(screen.getByText('0.84× its 50-day average (needs 1.1×).')).toBeTruthy()
    expect(screen.getByText('76%')).toBeTruthy()
    expect(screen.getAllByText('Met')).toHaveLength(2)
    expect(screen.getByText('Not met')).toBeTruthy()
    expect(screen.getByText('Show next steps')).toBeTruthy()
    expect(screen.getByText('Past limit')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'About Price vs the buy line' })).toBeTruthy()
  })

  it('shows window closed, >100% progress, moving-average stack strips, and past-zone cancellation', async () => {
    const expiredAndDip: StockRow = {
      ...zs,
      status: 'notCandidate',
      candidateSince: null,
      checks: [
        check({ key: 'beaten', group: 'candidate', met: false, value: 74, min: 30, max: 60, date: '2026-07-15' }),
        check({ key: 'gain', group: 'candidate', met: true, value: 118.9, min: 50, progress: 2.38 }),
        check({ key: 'stacked', group: 'candidate', met: true, value: 32.5, level: 41.2, midLevel: 38.5, endLevel: 31.1 }),
        check({ key: 'zone', group: 'buy', met: false, value: 26.7, min: 5, max: 15, level: 42.7, endLevel: 38.21 }),
      ],
    }
    open(strategy([expiredAndDip]))
    expect(await screen.findByText('NOT A CANDIDATE')).toBeTruthy()
    expect(screen.getByText('Window closed')).toBeTruthy()
    expect(screen.getByText('Window closed: last beaten down on Jul 15 (74 trading days ago; window is 60 days).')).toBeTruthy()
    expect(screen.getByText('238%')).toBeTruthy()
    expect(screen.getByText('Short $41.20')).toBeTruthy()
    expect(screen.getByText('Mid $38.50')).toBeTruthy()
    expect(screen.getByText('Long $31.10')).toBeTruthy()
    expect(screen.getByText('Past zone')).toBeTruthy()
    expect(screen.getByText('Fell past the discount zone: 26.7% below the peak; the zone is 5–15% ($42.70 to $38.21).')).toBeTruthy()
  })

  it('says a stock that is not a candidate gets no signal, and leads a waiting signal to Confirm', async () => {
    const not: StockRow = {
      ...zs, status: 'notCandidate', checks: [check({ key: 'beaten', group: 'candidate', met: false })],
    }
    open(strategy([not]))
    expect(await screen.findByText('NOT A CANDIDATE')).toBeTruthy()
    expect(screen.getByText('No: it gets no signal, however the price moves.')).toBeTruthy()
    cleanup()
    invalidateAll()

    open(strategy([{ ...zs, status: 'waitingForYou', signalId: 'sig', signalStep: 1 }]))
    expect((await screen.findByRole('link', { name: 'Answer the signal' })).getAttribute('href')).toBe('/strategies/s1/signals/sig')
  })

  it('waits for the daily check before showing the rules, and knows a stock outside the strategy', async () => {
    open(strategy([{ ...zs, checks: [] }]))
    expect(await screen.findByText('Each rule shows here after the next daily check.')).toBeTruthy()
    cleanup()
    invalidateAll()

    open(strategy([zs]), 'other')
    expect(await screen.findByText("This stock isn't in this strategy.")).toBeTruthy()
  })
})
