import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { TemplateKey } from '../../api'
import { initialDraft, withTemplate, type DraftPatch, type DraftState } from './draftState'
import { readStoredDraft, storeDraft } from './draftStorage'

export type { AiSource, Mode } from './draftState'

interface Draft extends DraftState {
  update: (patch: DraftPatch) => void
  /** Choose a template. A different one starts over from the defaults. */
  switchTemplate: (templateKey: TemplateKey) => void
  /** Replace the whole draft, e.g. with a simulation's settings ("Go live with these settings"). */
  load: (state: DraftState) => void
  /** Back to the defaults: call when a strategy is started. */
  reset: () => void
}

const DraftContext = createContext<Draft | null>(null)

// Kept in this tab's sessionStorage, so a reload mid-flow keeps the chosen template and choices; closing the tab drops
// it. It belongs to `owner` (the logged-in user's email, null for a guest, undefined while the account is still
// loading): a guest signing up or logging in keeps their draft; another user, or a log-out, starts it over, so a guest
// never sees the last user's draft. While the owner is unknown the draft is held as it is, so a reload doesn't reset it.
export function NewStrategyDraftProvider({ owner, children }: { owner: string | null | undefined; children: ReactNode }) {
  const [stored] = useState(readStoredDraft)
  const [state, setState] = useState<DraftState>(() => stored?.state ?? initialDraft)
  const [draftOwner, setDraftOwner] = useState<string | null>(() => (stored ? stored.owner : (owner ?? null)))
  if (owner !== undefined && owner !== draftOwner) {
    // Reset while rendering, not in an effect, so the next user never sees a frame of the last one's draft.
    setDraftOwner(owner)
    if (draftOwner !== null) {
      setState(initialDraft)
    }
  }

  useEffect(() => storeDraft({ owner: draftOwner, state }), [draftOwner, state])

  const update = useCallback((patch: DraftPatch) => setState((s) => ({ ...s, ...patch })), [])
  const switchTemplate = useCallback((key: TemplateKey) => setState((s) => withTemplate(s, key)), [])
  const load = useCallback((next: DraftState) => setState(next), [])
  const reset = useCallback(() => setState(initialDraft), [])
  const draft = useMemo(() => ({ ...state, update, switchTemplate, load, reset }), [state, update, switchTemplate, load, reset])
  return <DraftContext.Provider value={draft}>{children}</DraftContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useNewStrategyDraft(): Draft {
  const draft = useContext(DraftContext)
  if (!draft) throw new Error('useNewStrategyDraft must be used inside NewStrategyDraftProvider')
  return draft
}
