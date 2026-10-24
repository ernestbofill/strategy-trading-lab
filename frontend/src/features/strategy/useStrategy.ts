import { api, type StrategyDetail } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const strategyCacheKey = (id: string) => `strategy:${id}`

export const useStrategy = (id: string): Loadable<StrategyDetail> => useLoadable(strategyCacheKey(id), () => api.getStrategy(id))
