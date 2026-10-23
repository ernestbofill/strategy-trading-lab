import { useEffect, useReducer } from 'react'

export type Loadable<T> = { status: 'loading' } | { status: 'error' } | { status: 'missing' } | { status: 'ready'; data: T }

// Results live for the session, keyed by cache key, so going back to a screen shows it at once.
// Errors are kept only until the next mount, which retries.
const settled = new Map<string, Loadable<unknown>>()
const pending = new Map<string, Promise<void>>()

const LOADING = { status: 'loading' } as const
const MISSING = { status: 'missing' } as const

/** Forgets a result, and any request still in flight for it, so the next screen that asks loads it again. */
export function invalidate(cacheKey: string) {
  settled.delete(cacheKey)
  pending.delete(cacheKey)
}

/** Forgets every result: another user logged in (or out), so nothing loaded for the last one may show. */
export function invalidateAll() {
  settled.clear()
  pending.clear()
}

/** Loads `load()` once per `cacheKey`; `undefined` from `load` means not found. */
export function useLoadable<T>(cacheKey: string | null, load: () => Promise<T | undefined>): Loadable<T> {
  const [, rerender] = useReducer((n: number) => n + 1, 0)

  useEffect(() => {
    if (!cacheKey) return
    const cached = settled.get(cacheKey)
    if (cached && cached.status !== 'error') return
    settled.delete(cacheKey)

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
  }, [cacheKey])

  if (!cacheKey) return MISSING
  return (settled.get(cacheKey) as Loadable<T> | undefined) ?? LOADING
}
