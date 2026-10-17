import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { TemplateKey } from '../../api'
import { DEFAULT_BUDGET } from '../../lib/budget'

export type Mode = 'simulation' | 'live'

export type AiSource = 'news' | 'mood' | 'figures'

// Choices made across the new-strategy flow (1 / 5 … 5 / 5), kept while the user moves back and forth.
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
}

interface Draft extends DraftState {
  update: (patch: Partial<DraftState>) => void
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
}

// Survives a page reload within the tab; storage may be unavailable (private mode), so fail quietly.
const STORAGE_KEY = 'stl.newStrategyDraft'

function load(): DraftState {
  try {
    const saved = sessionStorage.getItem(STORAGE_KEY)
    return saved ? { ...initialDraft, ...JSON.parse(saved) } : initialDraft
  } catch {
    return initialDraft
  }
}

const DraftContext = createContext<Draft | null>(null)

export function NewStrategyDraftProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<DraftState>(load)

  useEffect(() => {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state))
    } catch {
      // ignore
    }
  }, [state])

  const update = useCallback((patch: Partial<DraftState>) => setState((s) => ({ ...s, ...patch })), [])
  const draft = useMemo(() => ({ ...state, update }), [state, update])
  return <DraftContext.Provider value={draft}>{children}</DraftContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useNewStrategyDraft(): Draft {
  const draft = useContext(DraftContext)
  if (!draft) throw new Error('useNewStrategyDraft must be used inside NewStrategyDraftProvider')
  return draft
}
