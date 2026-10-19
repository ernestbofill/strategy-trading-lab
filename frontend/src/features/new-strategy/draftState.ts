import type { TemplateKey } from '../../api'

export type Mode = 'simulation' | 'live'

export type AiSource = 'news' | 'mood' | 'figures'

// Choices made across the new-strategy flow (1 / 5 … 5 / 5) for the template being set up.
export interface DraftState {
  templateKey: TemplateKey
  /** Symbols the user unticked. */
  unticked: string[]
  aiSources: Record<AiSource, boolean>
  mode: Mode
  /** Euro amounts, see lib/budget.ts. Null until the user changes them, so they follow the template's tested defaults. */
  budget: number | null
  step: number | null
  maxSteps: number | null
  /** What the user typed on the last screen; null until then, so the name follows the template. */
  name: string | null
}

/** What `update` may change: everything but the template, which only `switchTemplate` changes. */
export type DraftPatch = Partial<Omit<DraftState, 'templateKey'>>

export const initialDraft: DraftState = {
  // Dip in winners is selected by default, so "Continue" is always enabled.
  templateKey: 'dip',
  unticked: [],
  aiSources: { news: true, mood: true, figures: true },
  mode: 'simulation',
  budget: null,
  step: null,
  maxSteps: null,
  name: null,
}

/** Another template starts over from the defaults; the same one keeps everything the user has set. */
export const withTemplate = (draft: DraftState, templateKey: TemplateKey): DraftState =>
  draft.templateKey === templateKey ? draft : { ...initialDraft, templateKey }
