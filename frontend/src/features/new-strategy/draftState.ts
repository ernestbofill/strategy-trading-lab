import type { Security, TemplateDetail, TemplateKey } from '../../api'

export type Mode = 'simulation' | 'live'

export type AiSource = 'news' | 'mood' | 'figures'

// Choices made across the new-strategy flow (1 / 5 … 5 / 5) for the template being set up.
export interface DraftState {
  templateKey: TemplateKey
  /** Symbols the user unticked. */
  unticked: string[]
  /** The fund the user picked instead of the template's (fund picker); null for the template's. */
  fund: Security | null
  aiSources: Record<AiSource, boolean>
  /** The prompt's instructions in the user's words (Edit prompt); null for the suggested ones. */
  aiInstructions: string | null
  mode: Mode
  /** Euro amounts, see lib/budget.ts. Null until the user changes them, so they follow the template's tested defaults. */
  budget: number | null
  step: number | null
  maxSteps: number | null
  /** Advanced settings the user changed, by setting key; the others follow the template's tested defaults. */
  settings: Record<string, number>
  /** What the user typed on the last screen; null until then, so the name follows the template. */
  name: string | null
}

/** What `update` may change: everything but the template, which only `switchTemplate` changes. */
export type DraftPatch = Partial<Omit<DraftState, 'templateKey'>>

export const initialDraft: DraftState = {
  // Winners at a discount is selected by default, so "Continue" is always enabled.
  templateKey: 'dip',
  unticked: [],
  fund: null,
  aiSources: { news: true, mood: true, figures: true },
  aiInstructions: null,
  mode: 'simulation',
  budget: null,
  step: null,
  maxSteps: null,
  settings: {},
  name: null,
}

/** Another template starts over from the defaults; the same one keeps everything the user has set. */
export const withTemplate = (draft: DraftState, templateKey: TemplateKey): DraftState =>
  draft.templateKey === templateKey ? draft : { ...initialDraft, templateKey }

/** The fund a fund template watches: the one the user picked, else the template's. */
export const fundOf = (draft: DraftState, template: TemplateDetail): Security | null =>
  template.watches.kind !== 'fund' ? null : draft.templateKey === template.key && draft.fund ? draft.fund : template.watches.fund
