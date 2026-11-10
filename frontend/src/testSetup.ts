import { afterEach } from 'vitest'

// jsdom has no scrolling: a new page's scroll to the top (App's ScrollToTop) would log "Not implemented".
if (typeof window !== 'undefined') window.scrollTo = () => {}

// The new-strategy draft is kept in sessionStorage: each test starts without the last one's.
afterEach(() => {
  if (typeof sessionStorage !== 'undefined') sessionStorage.clear()
})
