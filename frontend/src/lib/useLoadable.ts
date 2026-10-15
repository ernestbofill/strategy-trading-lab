import { useEffect, useReducer } from 'react'

export type Loadable<T> = { status: 'loading' } | { status: 'error' } | { status: 'missing' } | { status: 'ready'; data: T }

// Results live for the session, keyed by cache key, so going back to a screen shows it at once.
// Errors are kept only until the next mount, which retries.
const settled = new Map<string, Loadable<unknown>>()
const pending = new Map<string, Promise<void>>()

const LOADING = { status: 'loading' } as const
const MISSING = { status: 'missing' } as const

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
      request = load()
        .then(
          (data) => void settled.set(cacheKey, data === undefined ? MISSING : { status: 'ready', data }),
          () => void settled.set(cacheKey, { status: 'error' }),
        )
        .finally(() => pending.delete(cacheKey))
      pending.set(cacheKey, request)
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
