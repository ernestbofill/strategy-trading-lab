import { invalidate } from '../../lib/useLoadable'

/** What the admin pages load, by cache key (`useLoadable`). */
export const adminCacheKeys = {
  settings: 'admin:settings',
  alerts: 'admin:alerts',
  users: 'admin:users',
  invites: 'admin:invites',
  assets: 'admin:assets',
  templates: 'admin:templates',
  prices: 'admin:prices',
} as const

/** After a database switch every admin page shows the other database's rows: forget them all. */
export const invalidateAdmin = () => Object.values(adminCacheKeys).forEach(invalidate)
