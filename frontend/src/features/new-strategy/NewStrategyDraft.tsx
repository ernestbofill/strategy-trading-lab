import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import type { TemplateKey } from '../../api'
import { DEFAULT_BUDGET } from '../../lib/budget'

export type Mode = 'simulation' | 'live'

export type AiSource = 'news' | 'mood' | 'figures'

// Choices made across the new-strategy flow (1 / 5 … 5 / 5), kept while the user moves back and forth.
// Held in memory only: a new strategy, a reload or reopening the app starts from the defaults.
interface DraftState {
  templateKey: TemplateKey
  /** Symbols the user unticked, per template, so switching templates doesn't lose them. */
  unticked: Partial<Record<TemplateKey, string[]>>
  aiSources: Record<AiSource, boolean>
  mode: Mode
  /** Euro amounts, see lib/budget.ts. */
  budget: number
  step: number
  maxSteps: number
  /** What the user typed on the last screen; null until then, so the name follows the template. */
  name: string | null
  /** Set once the user has seen the confirm screen; only then do Edit links there return to it. */
  reachedConfirm: boolean
}

interface Draft extends DraftState {
  update: (patch: Partial<DraftState>) => void
  /** Back to the defaults: call when a strategy is started or a new one begins. */
  reset: () => void
  /** Start over with another template: everything goes back to the defaults. */
  switchTemplate: (templateKey: TemplateKey) => void
}

const initialDraft: DraftState = {
  // Dip in winners is selected by default, so "Continue" is always enabled.
  templateKey: 'dip',
  unticked: {},
  aiSources: { news: true, mood: true, figures: true },
  mode: 'simulation',
  budget: DEFAULT_BUDGET.budget,
  step: DEFAULT_BUDGET.step,
  maxSteps: DEFAULT_BUDGET.maxSteps,
  name: null,
  reachedConfirm: false,
}

const DraftContext = createContext<Draft | null>(null)

export function NewStrategyDraftProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<DraftState>(initialDraft)

  const update = useCallback((patch: Partial<DraftState>) => setState((s) => ({ ...s, ...patch })), [])
  const reset = useCallback(() => setState(initialDraft), [])
  const switchTemplate = useCallback(
    (templateKey: TemplateKey) => setState((s) => ({ ...initialDraft, templateKey, reachedConfirm: s.reachedConfirm })),
    [],
  )
  const draft = useMemo(() => ({ ...state, update, reset, switchTemplate }), [state, update, reset, switchTemplate])
  return <DraftContext.Provider value={draft}>{children}</DraftContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useNewStrategyDraft(): Draft {
  const draft = useContext(DraftContext)
  if (!draft) throw new Error('useNewStrategyDraft must be used inside NewStrategyDraftProvider')
  return draft
}
