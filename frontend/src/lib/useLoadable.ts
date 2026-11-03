import { useCallback, useEffect, useLayoutEffect, useRef, useState, useSyncExternalStore } from 'react'

export type Loadable<T> = { status: 'loading' } | { status: 'error' } | { status: 'missing' } | { status: 'ready'; data: T }

// Results are kept per cache key, so going back to a screen shows it at once. They are never trusted as current: the
// server changes data on its own (a new signal, an AI round due), so a screen that opens shows the kept result and
// loads it again in the background, and `refreshAll` reloads every open screen (the app back in front, a push).
const settled = new Map<string, Loadable<unknown>>()
const pending = new Map<string, Promise<void>>()
const loadedAt = new Map<string, number>()
// Keys the user changed (`invalidate`): a screen that opens on one shows loading, not the old result, until it reloads,
// and a failed reload shows the error instead of the result from before the change.
const stale = new Set<string>()
// The open screens on each key: the listener that re-renders each, with its latest loader.
const listeners = new Map<string, Map<() => void, () => Promise<unknown>>>()
const loaderOf = (cacheKey: string) => listeners.get(cacheKey)?.values().next().value
// Bumped to have the open screens on a key load it again from their effect, after React commits: a screen that the
// same change unmounts (log-out, navigating away from the screen just answered) doesn't load it for nothing.
const reloads = new Map<string, number>()

const LOADING = { status: 'loading' } as const
const MISSING = { status: 'missing' } as const
/** A result this young isn't loaded again when another screen opens on it (the dashboard and its cards). */
const FRESH_MS = 2000

const notify = (cacheKey: string) => listeners.get(cacheKey)?.forEach((_, listener) => listener())

function requestReload(cacheKey: string) {
  reloads.set(cacheKey, (reloads.get(cacheKey) ?? 0) + 1)
  notify(cacheKey)
}

/** Starts a load of `cacheKey` unless one is in flight. A failed background reload keeps the result already shown. */
function fetchKey(cacheKey: string, load: () => Promise<unknown>) {
  if (pending.has(cacheKey)) return
  // An invalidated request still finishes, but must not overwrite the newer one's result.
  const current = (): boolean => pending.get(cacheKey) === started
  const settle = (result: Loadable<unknown>, loaded: boolean) => {
    if (!current()) return
    pending.delete(cacheKey)
    stale.delete(cacheKey)
    if (loaded) loadedAt.set(cacheKey, Date.now())
    settled.set(cacheKey, result)
    notify(cacheKey)
  }
  const failed = (): Loadable<unknown> => {
    const shown = settled.get(cacheKey)
    return shown && !stale.has(cacheKey) ? shown : { status: 'error' }
  }
  const started: Promise<void> = load().then(
    (data) => settle(data === undefined ? MISSING : { status: 'ready', data }, true),
    () => settle(failed(), false),
  )
  pending.set(cacheKey, started)
}

/**
 * Forgets a result, and any request still in flight for it, so screens already showing it (or the next that asks)
 * load again. Use it after the user changes something the result shows.
 */
export function invalidate(cacheKey: string) {
  pending.delete(cacheKey)
  loadedAt.delete(cacheKey)
  stale.add(cacheKey)
  // Screens showing it keep it until the reload lands; with none open, the next screen loads it.
  if (listeners.has(cacheKey)) requestReload(cacheKey)
  else settled.delete(cacheKey)
}

/**
 * Forgets every result: another user logged in (or out), so nothing loaded for the last one may show. Screens that
 * are still open once React commits show loading and load again.
 */
export function invalidateAll() {
  settled.clear()
  pending.clear()
  loadedAt.clear()
  stale.clear()
  for (const cacheKey of listeners.keys()) requestReload(cacheKey)
}

/**
 * Reloads what the open screens show, keeping it on screen meanwhile: the server may have changed it. Results younger
 * than `olderThanMs` are left alone (a quick switch back to the app).
 */
export function refreshAll(olderThanMs = 0) {
  const now = Date.now()
  for (const cacheKey of listeners.keys()) {
    if (now - (loadedAt.get(cacheKey) ?? 0) >= olderThanMs) fetchKey(cacheKey, loaderOf(cacheKey)!)
  }
}

/** Loads `load()` for `cacheKey`, showing a kept result at once and reloading it; `undefined` from `load` means not found. */
export function useLoadable<T>(cacheKey: string | null, load: () => Promise<T | undefined>): Loadable<T> {
  // Reloads call the latest `load`, which may read the screen's current props.
  const loadRef = useRef(load)
  useLayoutEffect(() => void (loadRef.current = load))
  const subscribe = useCallback(
    (listener: () => void) => {
      if (!cacheKey) return () => {}
      let open = listeners.get(cacheKey)
      if (!open) listeners.set(cacheKey, (open = new Map()))
      open.set(listener, () => loadRef.current())
      return () => {
        open.delete(listener)
        if (open.size === 0) listeners.delete(cacheKey)
      }
    },
    [cacheKey],
  )
  const getSnapshot = useCallback(() => (cacheKey ? (settled.get(cacheKey) ?? LOADING) : MISSING), [cacheKey])
  const getReload = useCallback(() => (cacheKey ? (reloads.get(cacheKey) ?? 0) : 0), [cacheKey])
  const state = useSyncExternalStore(subscribe, getSnapshot)
  const reload = useSyncExternalStore(subscribe, getReload)
  // A screen that opens on a key the user changed waits for the reload; one already showing it keeps it meanwhile.
  const [shown, setShown] = useState<string | null>(null)
  const result = cacheKey && stale.has(cacheKey) && shown !== cacheKey ? LOADING : state
  if (result.status !== 'loading' && shown !== cacheKey) setShown(cacheKey)

  useEffect(() => {
    if (!cacheKey) return
    // An error is retried at once; a kept result is shown while it loads again, unless it was just loaded.
    const cached = settled.get(cacheKey)
    if (cached?.status === 'error') {
      settled.delete(cacheKey)
      notify(cacheKey)
    } else if (Date.now() - (loadedAt.get(cacheKey) ?? 0) < FRESH_MS) return
    fetchKey(cacheKey, loadRef.current)
  }, [cacheKey, reload])

  return result as Loadable<T>
}
