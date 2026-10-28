import type { Currency, NewStrategy, TemplateDetail } from '../../api'
import { resolveBudget } from '../../lib/budget'
import { fundOf, type DraftState } from './draftState'

/** What to send to start the strategy the user set up: their choices over the template's tested defaults. */
export function newStrategyFrom(draft: DraftState, template: TemplateDetail, name: string, currency: Currency): NewStrategy {
  const { watches, defaults } = template
  const { budget, step, maxSteps } = resolveBudget(draft, defaults.budget)
  // An all-in, all-out template has no steps (its tested default has none).
  const hasSteps = defaults.budget.step !== null
  return {
    templateKey: template.key,
    name,
    mode: draft.mode,
    currency,
    budget: { amount: budget, step: hasSteps ? step : null, maxSteps: hasSteps ? maxSteps : null },
    assetIds:
      watches.kind === 'fund'
        ? [fundOf(draft, template)!.id]
        : watches.starterList.filter((s) => !draft.unticked.includes(s.symbol)).map((s) => s.id),
    settings: draft.settings,
    // What the prompt asks for, for templates driven by AI rounds.
    ...(template.aiRound ? { ai: { ...draft.aiSources, instructions: draft.aiInstructions } } : {}),
  }
}

/** The template's name, numbered ("Winners at a discount 2") while a running strategy already has the name. Case is ignored. */
export function defaultName(templateName: string, taken: string[]): string {
  const used = new Set(taken.map((name) => name.trim().toLowerCase()))
  let name = templateName
  for (let n = 2; used.has(name.toLowerCase()); n++) name = `${templateName} ${n}`
  return name
}
