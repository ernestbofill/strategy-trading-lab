// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type BacktestDataStatus, type DrawReport } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { AdminBacktestData } from './AdminBacktestData'

const draw: DrawReport = {
  drawnAt: '2026-11-25T10:00:00Z',
  seed: 20261125,
  size: 60,
  membershipHash: 'abc',
  membershipLastRow: '2026-08-18',
  periods: [
    {
      label: '2011–14',
      start: '2011-01-01',
      end: '2014-12-31',
      partial: false,
      members: 497,
      eligible: 420,
      secondList: { members: 497, wikipediaMembers: 510, onlyFja: ['ABC'], onlyWikipedia: ['COR'] },
      sectors: [{ sector: 'Technology', eligible: 60, seats: 9 }],
      draft: [{ ticker: 'AAPL', symbol: 'AAPL', sector: 'Technology', third: 3 }],
      kept: 0,
      replaced: [],
      notes: [],
      representative: { draftRate: 0.09, allRate: 0.131, passed: false },
    },
  ],
  missing: [{ ticker: 'S', symbol: null, periods: ['2011–14'], reason: 'noSymbol', detail: 'Sprint Nextel was acquired (2013)' }],
  renamed: [],
  candidates: [{ template: 'dip', year: 2011, candidates: 9, signalled: 3, buySignals: 5, passed: false }],
  warnings: [],
  symbols: 709,
  snapshot: 'backtest/snapshots/2026-11/',
}

const status = (over: Partial<BacktestDataStatus>): BacktestDataStatus => ({
  storage: true,
  running: null,
  last: null,
  membership: { hash: 'abc', lastRow: '2026-08-18', changedSinceDraw: false },
  draw: null,
  snapshot: null,
  ...over,
})

const show = () =>
  render(
    <MemoryRouter>
      <AdminBacktestData />
    </MemoryRouter>,
  )

afterEach(() => {
  cleanup()
  invalidateAll()
  vi.restoreAllMocks()
})

describe('AdminBacktestData', () => {
  it('offers only a draw until the draft is drawn, and starts one', async () => {
    vi.spyOn(api, 'getAdminBacktestData').mockResolvedValue(status({}))
    const start = vi.spyOn(api, 'drawAdminBacktest').mockResolvedValue()
    show()

    const drawButton = await screen.findByRole('button', { name: 'Draw the test draft' })
    expect((screen.getByRole('button', { name: "Take this month's snapshot" }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.click(drawButton)
    expect(start).toHaveBeenCalled()
  })

  it('shows a running draw and the checks that failed', async () => {
    vi.spyOn(api, 'getAdminBacktestData').mockResolvedValue(
      status({ draw, running: { kind: 'draw', started: '2026-11-25T11:00:00Z', progress: { step: "Fetching the members' prices", done: 312, total: 709 } } }),
    )
    show()

    expect(await screen.findByText(/Fetching the members' prices: 312 of 709/)).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Drawing…' }) as HTMLButtonElement).disabled).toBe(true)
    expect(screen.getByText(/on the draft \+9\.0% a year; on every member that can be drawn \+13\.1%/).textContent).toMatch(/More than 2 points apart/)
    expect(screen.getByText('3 · 9 · 5')).toBeTruthy()
    expect(screen.getByRole('cell', { name: 'Another company today' })).toBeTruthy()
  })
})
