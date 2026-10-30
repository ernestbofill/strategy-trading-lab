import { api, type Dashboard } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const dashboardCacheKey = 'dashboard'

export const useDashboard = (enabled = true): Loadable<Dashboard> =>
  useLoadable(enabled ? dashboardCacheKey : null, api.getDashboard)
