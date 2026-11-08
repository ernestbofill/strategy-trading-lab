import { api, type DailySummary } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const summaryCacheKey = 'summary'

export const useSummary = (enabled = true): Loadable<DailySummary> =>
  useLoadable(enabled ? summaryCacheKey : null, api.getSummary)
