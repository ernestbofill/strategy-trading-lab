import { describe, expect, it } from 'vitest'
import type { StockRow } from '../../api'
import { stockLine } from './stockLine'

const row = (fields: Partial<StockRow>): StockRow => ({
  assetId: 'a',
  symbol: 'ZS',
  name: 'Zscaler',
  status: 'watching',
  steps: 0,
  rulesSteps: null,
  maxSteps: 5,
  distancePct: null,
  signalId: null,
  signalStep: null,
  invested: 0,
  result: null,
  ...fields,
})
const words = { zone: 'the discount zone', notCandidate: 'Below its long trend' }

describe('stockLine', () => {
  it('points a live signal to the user with its step', () => {
    const line = stockLine(row({ status: 'waitingForYou', signalStep: 2, rulesSteps: 1 }), 'live', 'EUR', words)
    expect([line.pill, line.detail]).toEqual(['WAITING FOR YOU', 'Signal: step 2 of 5'])
  })

  it('leaves out the step when a signal has none, and names a sale', () => {
    expect(stockLine(row({ status: 'waitingForYou' }), 'live', 'EUR', words).detail).toBe('Signal waiting')
    expect(stockLine(row({ status: 'signal' }), 'simulation', 'EUR', words).detail).toBe('Bought at the next open')
    expect(stockLine(row({ status: 'waitingForYou', signalSide: 'sell' }), 'live', 'EUR', words).detail).toBe('Sell signal waiting')
    expect(stockLine(row({ status: 'signal', signalSide: 'sell' }), 'simulation', 'EUR', words).detail).toBe('Sold at the next open')
  })

  it('shows the rules and the user apart only when their steps differ', () => {
    expect(stockLine(row({ status: 'building', steps: 2, rulesSteps: 3 }), 'live', 'EUR', words).detail).toBe(
      'Rules: 3 of 5 · You: 2 of 5',
    )
    expect(stockLine(row({ status: 'holding', steps: 5, rulesSteps: 5 }), 'live', 'EUR', words).detail).toBe('5 of 5 steps')
    expect(stockLine(row({ status: 'building', steps: 2 }), 'simulation', 'EUR', words).detail).toBe('2 of 5 steps')
  })

  it('formats bear drop statuses and trend out-of-the-market pills', () => {
    const bearWords = {
      zone: 'the bounce line',
      notCandidate: '{n}% below its 6-month high',
      watching: 'In a drop: {n}% above its low',
      holding: "This drop's steps are bought",
    }
    expect(stockLine(row({ status: 'notCandidate', distancePct: 4.2 }), 'simulation', 'EUR', bearWords).detail).toBe(
      '4% below its 6-month high',
    )
    expect(stockLine(row({ status: 'watching', distancePct: 2.3 }), 'simulation', 'EUR', bearWords).detail).toBe(
      'In a drop: 2% above its low',
    )
    const done = stockLine(row({ status: 'done', steps: 5 }), 'simulation', 'EUR', bearWords)
    expect([done.pill, done.detail]).toEqual(['DONE', "This drop's steps are bought"])

    const trendWords = { zone: 'the buy line', notCandidate: 'Below its long trend', holding: 'In the market' }
    const out = stockLine(row({ status: 'outOfTheMarket', maxSteps: null, distancePct: 1.8 }), 'simulation', 'EUR', trendWords)
    expect([out.pill, out.detail]).toEqual(['OUT OF THE MARKET', '2% from the buy line'])
    const holding = stockLine(row({ status: 'holding', maxSteps: null, steps: 1 }), 'simulation', 'EUR', trendWords)
    expect([holding.pill, holding.detail]).toEqual(['HOLDING', 'In the market'])
  })

  it("says how far a watched stock is, in the template's words", () => {
    expect(stockLine(row({ distancePct: 6.4 }), 'live', 'EUR', words).detail).toBe('6% from the discount zone')
    expect(stockLine(row({ distancePct: 0 }), 'live', 'EUR', words).detail).toBe('Rules not met today')
    expect(stockLine(row({ distancePct: 0.3 }), 'live', 'EUR', words).detail).toBe('<1% from the discount zone')
    const notCandidate = stockLine(row({ status: 'notCandidate' }), 'live', 'EUR', words)
    expect([notCandidate.pill, notCandidate.detail]).toEqual(['WATCHING', 'Below its long trend'])
    expect(stockLine(row({ status: 'noBudget' }), 'live', 'EUR', words).detail).toBe('No budget left for its steps')
  })

  it('shows the result with what was put in, or a dash with no buys', () => {
    const bought = stockLine(row({ status: 'building', steps: 1, invested: 101.2, result: -0.2 }), 'live', 'USD', words)
    expect([bought.result, bought.note]).toEqual(['−0.2%', 'You $101 in'])
    expect(stockLine(row({ status: 'building', steps: 1, invested: 100, result: 3 }), 'simulation', 'EUR', words).note).toBe('€100 in')
    const none = stockLine(row({}), 'simulation', 'EUR', words)
    expect([none.result, none.note]).toEqual(['–', 'No buys yet'])
  })

  it("words an AI strategy's stock by its last score, or not scored yet", () => {
    const ai = { zone: 'the top score', notCandidate: 'Not scored yet' }
    expect(stockLine(row({ status: 'watching', aiScore: 72 }), 'simulation', 'EUR', ai).detail).toBe('Last score: 72')
    expect(stockLine(row({ status: 'notCandidate' }), 'simulation', 'EUR', ai).detail).toBe('Not scored yet')
  })
})
