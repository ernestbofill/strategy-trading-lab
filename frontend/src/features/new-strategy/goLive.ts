import type { StrategyDetail, TemplateDetail } from '../../api'
import { initialDraft, type DraftState } from './draftState'

/**
 * "Go live with these settings": a draft holding a simulation's stocks, settings, budget, currency and (AI) prompt
 * choices, in live mode, named `name`. The draft keeps only what differs from the template, as the flow's screens do.
 */
export function draftFromStrategy(s: StrategyDetail, template: TemplateDetail, name: string): DraftState {
  const watched = new Set(s.stocks.map((row) => row.assetId))
  const { watches, defaults } = template
  const starter = watches.kind === 'stocks' ? watches.starterList : []
  const starterIds = new Set(starter.map((stock) => stock.id))
  const fundRow = watches.kind === 'fund' ? s.stocks.find((row) => row.assetId !== watches.fund.id) : undefined
  const settings = Object.fromEntries(
    Object.entries(s.settings ?? {}).filter(([key, value]) => defaults.params[key] !== value),
  )
  return {
    ...initialDraft,
    templateKey: template.key,
    unticked: starter.filter((stock) => !watched.has(stock.id)).map((stock) => stock.symbol),
    // Stocks it watches outside the starter list; their prices are in, as the simulation has run on them.
    added:
      watches.kind === 'stocks'
        ? s.stocks
            .filter((row) => !starterIds.has(row.assetId))
            .map((row) => ({ id: row.assetId, symbol: row.symbol, name: row.name, what: '', pricesIn: true }))
        : [],
    fund: fundRow ? { id: fundRow.assetId, symbol: fundRow.symbol, name: fundRow.name, what: '' } : null,
    ...(s.ai ? { aiSources: { news: s.ai.news, mood: s.ai.mood, figures: s.ai.figures }, aiInstructions: s.ai.instructions } : {}),
    mode: 'live',
    budget: s.budget.amount,
    step: s.budget.step,
    maxSteps: s.budget.maxSteps,
    settings,
    name,
    currency: s.currency,
    sourceStrategyId: s.id,
  }
}
