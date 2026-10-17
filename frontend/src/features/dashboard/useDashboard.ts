import { api, type Dashboard } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const useDashboard = (): Loadable<Dashboard> => useLoadable('dashboard', api.getDashboard)
