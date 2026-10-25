import { api, type AiRoundDetail, type AiRoundHistory } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const aiRoundCacheKey = (id: string) => `ai-round:${id}`
export const aiRoundsCacheKey = (id: string) => `ai-rounds:${id}`

/** A strategy's AI round that is due, or the next one. */
export const useAiRound = (id: string): Loadable<AiRoundDetail> => useLoadable(aiRoundCacheKey(id), () => api.getAiRound(id))

/** A strategy's AI round history. */
export const useAiRounds = (id: string): Loadable<AiRoundHistory> => useLoadable(aiRoundsCacheKey(id), () => api.getAiRounds(id))
