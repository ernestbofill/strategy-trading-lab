import { describe, expect, it } from 'vitest'
import type { Security, TemplateDetail } from '../../api'
import { initialDraft } from './draftState'
import { defaultName, newStrategyFrom } from './newStrategy'

const stock = (symbol: string): Security => ({ id: `id-${symbol}`, symbol, name: symbol, what: '' })

const dip = {
  key: 'dip',
  watches: { kind: 'stocks', starterList: [stock('NVDA'), stock('AVGO'), stock('META')] },
  defaults: { version: 1, budget: { amount: 3000, step: 100, maxSteps: 5 }, params: {} },
} as unknown as TemplateDetail

const trend = {
  key: 'trend',
  watches: { kind: 'fund', fund: stock('CSPX') },
  defaults: { version: 1, budget: { amount: 3000, step: null, maxSteps: null }, params: {} },
} as unknown as TemplateDetail

describe('newStrategyFrom', () => {
  it('starts from the tested defaults when the user changed nothing', () => {
    expect(newStrategyFrom(initialDraft, dip, 'Dip in winners', 'EUR')).toEqual({
      templateKey: 'dip',
      name: 'Dip in winners',
      mode: 'simulation',
      currency: 'EUR',
      budget: { amount: 3000, step: 100, maxSteps: 5 },
      assetIds: ['id-NVDA', 'id-AVGO', 'id-META'],
      settings: {},
    })
  })

  it('sends the settings the user changed', () => {
    const draft = { ...initialDraft, settings: { peakDays: 40 } }
    expect(newStrategyFrom(draft, dip, 'Mine', 'EUR').settings).toEqual({ peakDays: 40 })
  })

  it('sends the mode, the budget the user set and only the ticked stocks', () => {
    const draft = { ...initialDraft, mode: 'live' as const, budget: 6000, step: 200, maxSteps: 4, unticked: ['AVGO'] }
    expect(newStrategyFrom(draft, dip, 'Mine', 'USD')).toMatchObject({
      mode: 'live',
      currency: 'USD',
      budget: { amount: 6000, step: 200, maxSteps: 4 },
      assetIds: ['id-NVDA', 'id-META'],
    })
  })

  it('sends what the AI prompt asks for, for a template driven by AI rounds', () => {
    const ai = { ...dip, key: 'ai', aiRound: { everyWeeks: 2, topN: 1 } } as TemplateDetail
    const draft = { ...initialDraft, aiSources: { news: true, mood: false, figures: true }, aiInstructions: 'Score the AI products.' }
    expect(newStrategyFrom(draft, ai, 'Mine', 'EUR').ai).toEqual({ news: true, mood: false, figures: true, instructions: 'Score the AI products.' })
    expect(newStrategyFrom(draft, dip, 'Mine', 'EUR').ai).toBeUndefined()
  })

  it('sends the one fund, and no steps for an all-in template', () => {
    expect(newStrategyFrom({ ...initialDraft, budget: 5000 }, trend, 'Trend', 'EUR')).toMatchObject({
      budget: { amount: 5000, step: null, maxSteps: null },
      assetIds: ['id-CSPX'],
    })
  })
})

describe('defaultName', () => {
  it('is the template name while it is free', () => {
    expect(defaultName('Dip in winners', [])).toBe('Dip in winners')
    expect(defaultName('Dip in winners', ['Bear'])).toBe('Dip in winners')
  })

  it('numbers it from 2, taking the first free number and ignoring case', () => {
    expect(defaultName('Dip in winners', ['dip in winners'])).toBe('Dip in winners 2')
    expect(defaultName('Dip in winners', ['Dip in winners', 'Dip in winners 2'])).toBe('Dip in winners 3')
    expect(defaultName('Dip in winners', ['Dip in winners', 'Dip in winners 3'])).toBe('Dip in winners 2')
  })
})
