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
  checks: [
    check({ key: 'beaten', group: 'candidate', met: true, value: 12, min: 30, max: 60 }),
    check({ key: 'drop', group: 'candidate', met: null, value: -29.2, min: 30, level: 335.77 }),
    check({ key: 'buyLine', group: 'buy', met: true, value: 1.6, min: 15, max: 8, level: 233.9 }),
    check({ key: 'volume', group: 'buy', met: false, value: 0.84, min: 1.1, progress: 0.76 }),
  ],
}

const strategy = (stocks: StockRow[]): StrategyDetail => ({
  id: 's1', name: 'Comebacks', templateKey: 'comeback', templateName: 'The comeback', investsIn: 'stockList', mode: 'live',
  status: 'running', endedOn: null, notifications: true, startedOn: '2026-10-13', today: '2026-10-31', currency: 'EUR',
  budget: { amount: 3000, used: 0, step: 100, maxSteps: 5, steps: 0, totalSteps: 30 }, results: null, pastResult: null,
  signals: { did: 0, skipped: 0, missed: 0 }, words: { zone: 'the buy line', notCandidate: 'Not beaten down' },
  stocks, trades: [], signalHistory: [], rates: [],
  checkTexts: {
    beaten: { label: 'Beaten down lately', detail: 'Last beaten down {value} trading days ago.', short: 'not beaten down lately' },
    drop: { label: 'Drop from the 52-week high', detail: '{+value}% from the 52-week high ({level}).', short: '' },
    buyLine: { label: 'Price vs the buy line', detail: '{+value}% vs the buy line ({level}).', short: '', info: 'The SMA 50 + {min}%.' },
    volume: { label: 'Volume: 5-day vs 50-day average', detail: '{value}× its 50-day average (needs {min}×).', short: 'volume {value}× (needs {min}×)' },
  },
})

function open(s: StrategyDetail, assetId = 'zs') {
  vi.spyOn(api, 'getStrategy').mockResolvedValue(s)
  render(
    <MemoryRouter initialEntries={[`/strategies/s1/stocks/${assetId}`]}>
      <Routes>
        <Route path="/strategies/:id/stocks/:assetId" element={<StockPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('StockPage', () => {
  it('separates what makes a candidate from what gives the signal, and says how far each rule is', async () => {
    open(strategy([zs]))

    expect(await screen.findByRole('heading', { name: 'ZS' })).toBeTruthy()
    expect(screen.getByText('CANDIDATE')).toBeTruthy()
    expect(screen.getByText('Volume 0.84× (needs 1.1×)')).toBeTruthy()
    expect(screen.getByText('Close $237.62 on Oct 30')).toBeTruthy()
    expect(screen.getByText('Yes: the rules watch it for a buy signal.')).toBeTruthy()
    expect(screen.getByText('1 of 2 met. All of them: a buy signal.')).toBeTruthy()
    expect(screen.getByText('−29.2% from the 52-week high ($335.77).')).toBeTruthy()
    expect(screen.getByText('+1.6% vs the buy line ($233.90).')).toBeTruthy()
    expect(screen.getByText('0.84× its 50-day average (needs 1.1×).')).toBeTruthy()
    expect(screen.getByText('76%')).toBeTruthy()
    expect(screen.getAllByText('Met')).toHaveLength(2)
    expect(screen.getByText('Not met')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'About Price vs the buy line' })).toBeTruthy()
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
