import { describe, expect, it } from 'vitest'
import type { Security, StockRow, StrategyDetail, TemplateDetail } from '../../api'
import { draftFromStrategy } from './goLive'

const stock = (symbol: string): Security => ({ id: `id-${symbol}`, symbol, name: `${symbol} Inc`, what: '' })
const row = (symbol: string): StockRow => ({
  assetId: `id-${symbol}`, symbol, name: `${symbol} Inc`, status: 'watching', steps: 0, rulesSteps: null, maxSteps: 5,
  distancePct: null, signalId: null, signalStep: null, invested: 0, result: null,
  exchange: 'NASDAQ', currency: 'USD', what: null, checkedOn: null, close: null, checks: [],
})

const dip = {
  key: 'dip',
  watches: { kind: 'stocks', starterList: [stock('NVDA'), stock('AVGO'), stock('META')] },
  defaults: { version: 1, budget: { amount: 3000, step: 100, maxSteps: 5 }, params: { peakDays: 60, dropPct: 20 } },
} as unknown as TemplateDetail

const trend = {
  key: 'trend',
  watches: { kind: 'fund', fund: stock('CSPX') },
  defaults: { version: 1, budget: { amount: 3000, step: null, maxSteps: null }, params: {} },
} as unknown as TemplateDetail

const simulation = (fields: Partial<StrategyDetail>) =>
  ({
    id: 'sim-1',
    name: 'Zscaler & friends',
    templateKey: 'dip',
    mode: 'simulation',
    status: 'running',
    currency: 'USD',
    budget: { amount: 6000, used: 0, step: 200, maxSteps: 4, steps: 0, totalSteps: 30 },
    stocks: [row('NVDA'), row('META'), row('ZS')],
    settings: { peakDays: 40, dropPct: 20 },
    ...fields,
  }) as StrategyDetail

describe('draftFromStrategy', () => {
  it("copies a simulation's stocks, changed settings, budget and currency into a live draft", () => {
    const draft = draftFromStrategy(simulation({}), dip, 'Zscaler & friends live')

    expect(draft).toMatchObject({
      templateKey: 'dip',
      unticked: ['AVGO'],
      added: [{ id: 'id-ZS', symbol: 'ZS', name: 'ZS Inc', what: '', pricesIn: true }],
      fund: null,
      mode: 'live',
      budget: 6000,
      step: 200,
      maxSteps: 4,
      settings: { peakDays: 40 },
      name: 'Zscaler & friends live',
      currency: 'USD',
      sourceStrategyId: 'sim-1',
    })
  })

  it("keeps a fund template's fund only when it isn't the template's", () => {
    const own = simulation({ templateKey: 'trend', stocks: [row('VUAA')], budget: { amount: 3000, used: 0, step: null, maxSteps: null, steps: null, totalSteps: null } })
    expect(draftFromStrategy(own, trend, 'x').fund).toMatchObject({ id: 'id-VUAA', symbol: 'VUAA' })
    expect(draftFromStrategy({ ...own, stocks: [row('CSPX')] }, trend, 'x').fund).toBeNull()
    expect(draftFromStrategy(own, trend, 'x')).toMatchObject({ unticked: [], added: [], step: null, maxSteps: null })
  })

  it("copies an AI strategy's prompt choices", () => {
    const ai = simulation({ ai: { news: true, mood: false, figures: true, instructions: 'Score the AI products.' } })
    expect(draftFromStrategy(ai, dip, 'x')).toMatchObject({
      aiSources: { news: true, mood: false, figures: true },
      aiInstructions: 'Score the AI products.',
    })
  })
})
