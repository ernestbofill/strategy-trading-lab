import { afterEach, vi } from 'vitest'
import { invalidateAll } from './lib/useLoadable'

const dom = typeof window !== 'undefined'
// Loaded before any test stubs a global (Testing Library reads `navigator` as it loads), and only in jsdom files.
const testingLibrary = dom ? await import('@testing-library/react') : null

// jsdom has no scrolling: a new page's scroll to the top (App's ScrollToTop) would log "Not implemented".
if (dom) window.scrollTo = () => {}

// Every test's teardown, in one place: unmount (only in jsdom files, so the Node ones don't load React DOM), undo spies and
// stubbed globals, forget the new-strategy draft (sessionStorage) and every kept load. A file's own afterEach runs first.
afterEach(() => {
  testingLibrary?.cleanup()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  if (dom) sessionStorage.clear()
  invalidateAll()
})
