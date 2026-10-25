import { useCallback, useEffect, useReducer, useSyncExternalStore } from 'react'

export type Loadable<T> = { status: 'loading' } | { status: 'error' } | { status: 'missing' } | { status: 'ready'; data: T }

// Results live for the session, keyed by cache key, so going back to a screen shows it at once.
// Errors are kept only until the next mount, which retries.
const settled = new Map<string, Loadable<unknown>>()
const pending = new Map<string, Promise<void>>()
const stale = new Set<string>()

const LOADING = { status: 'loading' } as const
const MISSING = { status: 'missing' } as const

const keyGenerations = new Map<string, number>()
const keyListeners = new Map<string, Set<() => void>>()

const subscribeKey = (cacheKey: string | null, listener: () => void) => {
  if (!cacheKey) return () => {}
  let listeners = keyListeners.get(cacheKey)
  if (!listeners) {
    listeners = new Set()
    keyListeners.set(cacheKey, listeners)
  }
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
    if (listeners.size === 0) {
      keyListeners.delete(cacheKey)
      keyGenerations.delete(cacheKey)
    }
  }
}

const getKeyGeneration = (cacheKey: string | null): number => {
  if (!cacheKey) return 0
  return keyGenerations.get(cacheKey) ?? 0
}

/**
 * Forgets a result, and any request still in flight for it, so screens already showing it (or the next that asks)
 * load again.
 */
export function invalidate(cacheKey: string) {
  pending.delete(cacheKey)
  stale.add(cacheKey)
  const listeners = keyListeners.get(cacheKey)
  if (!listeners || listeners.size === 0) {
    settled.delete(cacheKey)
  }
  keyGenerations.set(cacheKey, (keyGenerations.get(cacheKey) ?? 0) + 1)
  if (listeners) {
    for (const listener of [...listeners]) listener()
  }
}

/** Bumped by `invalidateAll`; screens already showing data load again when it changes. */
let generation = 0
const generationListeners = new Set<() => void>()
const subscribeGeneration = (listener: () => void) => {
  generationListeners.add(listener)
  return () => void generationListeners.delete(listener)
}
const currentGeneration = () => generation

/**
 * Forgets every result: another user logged in (or out), so nothing loaded for the last one may show. Screens that
 * are open load again.
 */
export function invalidateAll() {
  settled.clear()
  pending.clear()
  stale.clear()
  generation++
  for (const listener of generationListeners) listener()
}

/** Loads `load()` once per `cacheKey`; `undefined` from `load` means not found. */
export function useLoadable<T>(cacheKey: string | null, load: () => Promise<T | undefined>): Loadable<T> {
  const [, rerender] = useReducer((n: number) => n + 1, 0)
  const loadGeneration = useSyncExternalStore(subscribeGeneration, currentGeneration)
  const subscribe = useCallback((onStoreChange: () => void) => subscribeKey(cacheKey, onStoreChange), [cacheKey])
  const getSnapshot = useCallback(() => getKeyGeneration(cacheKey), [cacheKey])
  const keyGeneration = useSyncExternalStore(subscribe, getSnapshot)

  useEffect(() => {
    if (!cacheKey) return
    const isStale = stale.has(cacheKey)
    stale.delete(cacheKey)

    const cached = settled.get(cacheKey)
    if (cached && cached.status !== 'error' && !isStale) return
    if (isStale && (!cached || cached.status === 'error')) {
      settled.delete(cacheKey)
    }

    let request = pending.get(cacheKey)
    if (!request) {
      // An invalidated request still finishes, but must not overwrite the newer one's result.
      const current = (): boolean => pending.get(cacheKey) === started
      const started: Promise<void> = load()
        .then(
          (data) => void (current() && settled.set(cacheKey, data === undefined ? MISSING : { status: 'ready', data })),
          () => void (current() && settled.set(cacheKey, { status: 'error' })),
        )
        .finally(() => void (current() && pending.delete(cacheKey)))
      pending.set(cacheKey, started)
      request = started
    }
    let live = true
    request.then(() => live && rerender())
    return () => {
      live = false
    }
    // `load` is identified by `cacheKey`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cacheKey, loadGeneration, keyGeneration])

  if (!cacheKey) return MISSING
  return (settled.get(cacheKey) as Loadable<T> | undefined) ?? LOADING
}
