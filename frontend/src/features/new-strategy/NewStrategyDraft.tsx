import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import type { TemplateKey } from '../../api'
import { initialDraft, withTemplate, type DraftPatch, type DraftState } from './draftState'

export type { AiSource, Mode } from './draftState'

interface Draft extends DraftState {
  update: (patch: DraftPatch) => void
  /** Choose a template. A different one starts over from the defaults. */
  switchTemplate: (templateKey: TemplateKey) => void
  /** Back to the defaults: call when a strategy is started. */
  reset: () => void
}

const DraftContext = createContext<Draft | null>(null)

// Held in memory only: a reload or reopening the app starts from the defaults. It belongs to `owner` (the user's
// email): another user logging in on this tab starts it over.
export function NewStrategyDraftProvider({ owner, children }: { owner: string | null; children: ReactNode }) {
  const [state, setState] = useState<DraftState>(initialDraft)
  const [draftOwner, setDraftOwner] = useState(owner)
  if (owner !== draftOwner) {
    // Reset while rendering, not in an effect, so the next user never sees a frame of the last one's draft.
    setDraftOwner(owner)
    setState(initialDraft)
  }

  const update = useCallback((patch: DraftPatch) => setState((s) => ({ ...s, ...patch })), [])
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
