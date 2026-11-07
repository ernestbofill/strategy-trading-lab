import { useLocation, useNavigate } from 'react-router-dom'

/** Marks a history entry reached by going up: the screen before it isn't one the user came from. */
export interface UpState {
  up?: boolean
}

/**
 * Whether the screen before this one is one the user came from in the app.
 * The first entry of the history has the key 'default': there is nothing to go back to.
 * An entry reached by going up has nothing of the app's before it either.
 */
export function useHasHistory() {
  const location = useLocation()
  return location.key !== 'default' && !(location.state as UpState | null)?.up
}

/**
 * Leaves a screen after the user saves it: back to the screen they came from, or up to `parent`
 * (replacing the entry) when it was opened directly. Navigating forward instead would leave the
 * saved screen behind, so BACK on the parent would return to it.
 */
export function useLeave(parent: string) {
  const navigate = useNavigate()
  const hasHistory = useHasHistory()
  return () => {
    if (hasHistory) navigate(-1)
    else navigate(parent, { replace: true, state: { up: true } satisfies UpState })
  }
}
