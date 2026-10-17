import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import type { TemplateKey } from '../../api'
import { initialDraft, withTemplate, type DraftState } from './draftState'

export type { AiSource, Mode } from './draftState'

interface Draft extends DraftState {
  update: (patch: Partial<DraftState>) => void
  /** Choose a template. A different one starts over from the defaults. */
  switchTemplate: (templateKey: TemplateKey) => void
  /** Back to the defaults: call when a strategy is started. */
  reset: () => void
}

const DraftContext = createContext<Draft | null>(null)

// Held in memory only: a reload or reopening the app starts from the defaults.
export function NewStrategyDraftProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<DraftState>(initialDraft)

  const update = useCallback((patch: Partial<DraftState>) => setState((s) => ({ ...s, ...patch })), [])
  const switchTemplate = useCallback((key: TemplateKey) => setState((s) => withTemplate(s, key)), [])
  const reset = useCallback(() => setState(initialDraft), [])
  const draft = useMemo(() => ({ ...state, update, switchTemplate, reset }), [state, update, switchTemplate, reset])
  return <DraftContext.Provider value={draft}>{children}</DraftContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useNewStrategyDraft(): Draft {
  const draft = useContext(DraftContext)
  if (!draft) throw new Error('useNewStrategyDraft must be used inside NewStrategyDraftProvider')
  return draft
}
