import { afterEach } from 'vitest'

// The new-strategy draft is kept in sessionStorage: each test starts without the last one's.
afterEach(() => {
  if (typeof sessionStorage !== 'undefined') sessionStorage.clear()
})
