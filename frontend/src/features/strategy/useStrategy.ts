import { useEffect, useRef } from 'react'
import { api, type StrategyDetail } from '../../api'
import { reload, useLoadable, type Loadable } from '../../lib/useLoadable'

export const strategyCacheKey = (id: string) => `strategy:${id}`

/** How often, and how many times, a strategy with a stock not checked yet is asked again: the server checks it at once. */
const CHECK_POLL_MS = 5000
const CHECK_POLLS = 12

/** A running strategy with a stock its rules haven't checked yet (just started, or just added to the list). */
export const awaitsCheck = (s: StrategyDetail) =>
  s.status === 'running' && !s.aiRounds && s.stocks.some((row) => row.status === 'notChecked')

/**
 * A strategy, asked again every few seconds for a minute while a stock waits for its first check, so the check the
 * server makes on a start or a list save shows without a reload.
 */
export function useStrategy(id: string): Loadable<StrategyDetail> {
  const strategy = useLoadable(strategyCacheKey(id), () => api.getStrategy(id))
  const polls = useRef(0)
  const waiting = strategy.status === 'ready' && awaitsCheck(strategy.data)
  useEffect(() => {
    if (!waiting || polls.current >= CHECK_POLLS) return
    const timer = setTimeout(() => {
      polls.current++
      reload(strategyCacheKey(id))
    }, CHECK_POLL_MS)
    return () => clearTimeout(timer)
  }, [waiting, id, strategy])
  return strategy
}
