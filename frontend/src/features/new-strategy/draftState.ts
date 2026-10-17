import type { TemplateKey } from '../../api'
import { DEFAULT_BUDGET } from '../../lib/budget'

export type Mode = 'simulation' | 'live'

export type AiSource = 'news' | 'mood' | 'figures'

// Choices made across the new-strategy flow (1 / 5 … 5 / 5) for the template being set up.
export interface DraftState {
  templateKey: TemplateKey
  /** Symbols the user unticked. */
  unticked: string[]
  aiSources: Record<AiSource, boolean>
  mode: Mode
  /** Euro amounts, see lib/budget.ts. */
  budget: number
  step: number
  maxSteps: number
  /** What the user typed on the last screen; null until then, so the name follows the template. */
  name: string | null
  /** The user is changing something from the confirm screen: Back and Continue return there. */
  editing: boolean
}

export const initialDraft: DraftState = {
  // Dip in winners is selected by default, so "Continue" is always enabled.
  templateKey: 'dip',
  unticked: [],
  aiSources: { news: true, mood: true, figures: true },
  mode: 'simulation',
  budget: DEFAULT_BUDGET.budget,
  step: DEFAULT_BUDGET.step,
  maxSteps: DEFAULT_BUDGET.maxSteps,
  name: null,
  editing: false,
}

/** Another template starts over from the defaults; the same one keeps everything the user has set. */
export const withTemplate = (draft: DraftState, templateKey: TemplateKey): DraftState =>
  draft.templateKey === templateKey ? draft : { ...initialDraft, templateKey }
