import { api, type Dashboard } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const dashboardCacheKey = 'dashboard'

export const useDashboard = (): Loadable<Dashboard> => useLoadable(dashboardCacheKey, api.getDashboard)
