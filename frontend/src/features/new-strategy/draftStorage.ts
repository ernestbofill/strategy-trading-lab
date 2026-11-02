import { initialDraft, type DraftState } from './draftState'

/** The draft as kept in this tab: whose it is (null for a guest) and the choices made so far. */
export interface StoredDraft {
  owner: string | null
  state: DraftState
}

// Bump when DraftState changes in a way an older stored draft can't be read as: that one is then dropped.
const STORAGE_KEY = 'stl.newStrategyDraft.v1'

/**
 * The draft kept in this tab's sessionStorage, so a reload mid-flow keeps the chosen template and choices. Null when
 * there is none, it can't be read, or storage is blocked. Fields added since it was saved take their defaults.
 */
export function readStoredDraft(): StoredDraft | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (!isRecord(parsed) || !isRecord(parsed.state)) return null
    const { owner, state } = parsed
    if ((owner !== null && typeof owner !== 'string') || typeof state.templateKey !== 'string') return null
    return { owner, state: { ...initialDraft, ...state } as DraftState }
  } catch {
    return null
  }
}

/** Keeps the draft for this tab; does nothing where storage is blocked or full. */
export function storeDraft(draft: StoredDraft): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(draft))
  } catch {
    // Private mode or a full quota: the draft then lasts until the next reload, as before.
  }
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)
