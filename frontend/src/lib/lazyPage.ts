import { lazy, type ComponentType } from 'react'

const reloadKey = 'stl.chunkReload'

/**
 * `lazy()` for a page in its own chunk. A deploy removes the old chunks, so an app still running the previous build
 * (the service worker's copy) asks for one the server no longer has: reload once onto the new build instead of failing.
 * A second failure in a row is thrown, for `LazyPage` to show.
 */
export function lazyPage(load: () => Promise<ComponentType>) {
  return lazy(async () => {
    try {
      const page = await load()
      session(() => sessionStorage.removeItem(reloadKey))
      return { default: page }
    } catch (error) {
      // Blocked storage reads as already reloaded: a reload loop couldn't be stopped without it.
      const reloaded = session(() => sessionStorage.getItem(reloadKey)) !== null
      if (!reloaded && session(() => sessionStorage.setItem(reloadKey, '1')) !== false) {
        window.location.reload()
        return new Promise<never>(() => {})
      }
      throw error
    }
  })
}

/** Runs a sessionStorage call, or gives false where storage is blocked. */
function session<T>(call: () => T): T | false {
  try {
    return call()
  } catch {
    return false
  }
}
