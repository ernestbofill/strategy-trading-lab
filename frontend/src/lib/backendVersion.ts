import { useSyncExternalStore } from 'react'
import { api, type BackendVersion } from '../api'

// The API's build and database, fetched once per page load and shared by every top bar. The admin page refreshes it
// after a database switch, so the DEV tag follows.

let current: BackendVersion | null = null
let requested = false
const listeners = new Set<() => void>()

/** Fetches /version again. Keeps the last answer when the API can't be reached. */
export async function refreshBackendVersion(): Promise<void> {
  requested = true
  try {
    current = await api.getVersion()
  } catch {
    // The top bar shows nothing extra; the admin page shows its own error.
  }
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  if (!requested) void refreshBackendVersion()
  return () => listeners.delete(listener)
}

/** The API's version and database, or null until (or unless) it answers. */
export function useBackendVersion(): BackendVersion | null {
  return useSyncExternalStore(subscribe, () => current)
}
