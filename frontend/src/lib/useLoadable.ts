import { useCallback, useEffect, useSyncExternalStore } from 'react'

export type Loadable<T> = { status: 'loading' } | { status: 'error' } | { status: 'missing' } | { status: 'ready'; data: T }

// Results are kept per cache key, so going back to a screen shows it at once. They are never trusted as current: the
// server changes data on its own (a new signal, an AI round due), so a screen that opens shows the kept result and
// loads it again in the background, and `refreshAll` reloads every open screen (the app back in front, a push).
const settled = new Map<string, Loadable<unknown>>()
const pending = new Map<string, Promise<void>>()
const loadedAt = new Map<string, number>()
// The open screens: their loaders and the listeners that re-render them.
const loaders = new Map<string, () => Promise<unknown>>()
const listeners = new Map<string, Set<() => void>>()

const LOADING = { status: 'loading' } as const
const MISSING = { status: 'missing' } as const
/** A result this young isn't loaded again when another screen opens on it (the dashboard and its cards). */
const FRESH_MS = 2000

const notify = (cacheKey: string) => listeners.get(cacheKey)?.forEach((listener) => listener())

/** Starts a load of `cacheKey` unless one is in flight. A failed reload keeps the result already shown. */
function fetchKey(cacheKey: string, load: () => Promise<unknown>) {
  if (pending.has(cacheKey)) return
  // An invalidated request still finishes, but must not overwrite the newer one's result.
  const current = (): boolean => pending.get(cacheKey) === started
  const settle = (result: Loadable<unknown>) => {
    if (!current()) return
    pending.delete(cacheKey)
    loadedAt.set(cacheKey, Date.now())
    settled.set(cacheKey, result)
    notify(cacheKey)
  }
  const started: Promise<void> = load().then(
    (data) => settle(data === undefined ? MISSING : { status: 'ready', data }),
    () => settle(settled.get(cacheKey)?.status === 'ready' ? settled.get(cacheKey)! : { status: 'error' }),
  )
  pending.set(cacheKey, started)
}

/**
 * Forgets a result, and any request still in flight for it, so screens already showing it (or the next that asks)
 * load again. Use it after the user changes something the result shows.
 */
export function invalidate(cacheKey: string) {
  pending.delete(cacheKey)
  const load = loaders.get(cacheKey)
  if (load) fetchKey(cacheKey, load)
  else {
    settled.delete(cacheKey)
    loadedAt.delete(cacheKey)
  }
}

/**
 * Forgets every result: another user logged in (or out), so nothing loaded for the last one may show. Screens that
 * are open show loading and load again.
 */
export function invalidateAll() {
  settled.clear()
  pending.clear()
  loadedAt.clear()
  for (const [cacheKey, load] of loaders) {
    fetchKey(cacheKey, load)
    notify(cacheKey)
  }
}

/** Reloads what the open screens show, keeping it on screen meanwhile: the server may have changed it. */
export function refreshAll() {
  for (const [cacheKey, load] of loaders) fetchKey(cacheKey, load)
}

/** Loads `load()` for `cacheKey`, showing a kept result at once and reloading it; `undefined` from `load` means not found. */
export function useLoadable<T>(cacheKey: string | null, load: () => Promise<T | undefined>): Loadable<T> {
  const subscribe = useCallback(
    (listener: () => void) => {
      if (!cacheKey) return () => {}
      let set = listeners.get(cacheKey)
      if (!set) listeners.set(cacheKey, (set = new Set()))
      set.add(listener)
      return () => {
        set.delete(listener)
        if (set.size === 0) {
          listeners.delete(cacheKey)
          loaders.delete(cacheKey)
        }
      }
    },
    [cacheKey],
  )
  const getSnapshot = useCallback(() => (cacheKey ? (settled.get(cacheKey) ?? LOADING) : MISSING), [cacheKey])
  const state = useSyncExternalStore(subscribe, getSnapshot)

  useEffect(() => {
    if (!cacheKey) return
    loaders.set(cacheKey, load)
    // An error is retried at once; a kept result is shown while it loads again, unless it was just loaded.
    const cached = settled.get(cacheKey)
    if (cached?.status === 'error') settled.delete(cacheKey)
    if (cached?.status !== 'error' && Date.now() - (loadedAt.get(cacheKey) ?? 0) < FRESH_MS) return
    fetchKey(cacheKey, load)
    if (cached?.status === 'error') notify(cacheKey)
    // `load` is identified by `cacheKey`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cacheKey])

  return state as Loadable<T>
}
