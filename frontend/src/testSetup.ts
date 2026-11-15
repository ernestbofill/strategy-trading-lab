import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import { invalidateAll } from './lib/useLoadable'

// jsdom has no scrolling: a new page's scroll to the top (App's ScrollToTop) would log "Not implemented".
if (typeof window !== 'undefined') window.scrollTo = () => {}

// The new-strategy draft is kept in sessionStorage: each test starts without the last one's.
// Each test starts on an empty page, and loads its own results (they are kept per cache key).
afterEach(() => {
  cleanup()
  if (typeof sessionStorage !== 'undefined') sessionStorage.clear()
  invalidateAll()
})
