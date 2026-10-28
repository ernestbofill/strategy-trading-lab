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
    expect(newStrategyFrom(initialDraft, dip, 'Winners at a discount', 'EUR')).toEqual({
      templateKey: 'dip',
      name: 'Winners at a discount',
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
    const ai = { ...dip, key: 'ai', aiRound: { everyDays: 14, topN: 1 } } as TemplateDetail
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

  it('sends the fund the user picked instead of the tested one', () => {
    const draft = { ...initialDraft, templateKey: 'trend' as const, fund: stock('QQQ') }
    expect(newStrategyFrom(draft, trend, 'Trend', 'EUR').assetIds).toEqual(['id-QQQ'])
  })
})

describe('defaultName', () => {
  it('is the template name while it is free', () => {
    expect(defaultName('Winners at a discount', [])).toBe('Winners at a discount')
    expect(defaultName('Winners at a discount', ['Bear'])).toBe('Winners at a discount')
  })

  it('numbers it from 2, taking the first free number and ignoring case', () => {
    expect(defaultName('Winners at a discount', ['winners at a discount'])).toBe('Winners at a discount 2')
    expect(defaultName('Winners at a discount', ['Winners at a discount', 'Winners at a discount 2'])).toBe('Winners at a discount 3')
    expect(defaultName('Winners at a discount', ['Winners at a discount', 'Winners at a discount 3'])).toBe('Winners at a discount 2')
  })
})
