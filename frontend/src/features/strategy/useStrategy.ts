import { useEffect } from 'react'
import { api, type StrategyDetail } from '../../api'
import { reload, useLoadable, type Loadable } from '../../lib/useLoadable'

export const strategyCacheKey = (id: string) => `strategy:${id}`

/** How often, and how many times, a strategy with a stock not checked yet is asked again: the server checks it at once. */
const CHECK_POLL_MS = 5000
const CHECK_POLLS = 12

/**
 * A running strategy with a stock its rules haven't checked yet (just started, or just added to the list), not one the
 * server left to the daily run after a US session (`afterClose`).
 */
export const awaitsCheck = (s: StrategyDetail) =>
  s.status === 'running' && !s.aiRounds && s.stocks.some((row) => row.status === 'notChecked' && !row.afterClose)

/**
 * A strategy, asked again every few seconds for a minute while a stock waits for its first check, so the check the
 * server makes on a start or a list save shows without a reload.
 */
export function useStrategy(id: string): Loadable<StrategyDetail> {
  const strategy = useLoadable(strategyCacheKey(id), () => api.getStrategy(id))
  const waiting = strategy.status === 'ready' && awaitsCheck(strategy.data)
  // On a timer, not after each load: a failed reload keeps the result shown, and must not end the polling.
  useEffect(() => {
    if (!waiting) return
    let polls = 0
    const timer = setInterval(() => {
      if (++polls >= CHECK_POLLS) clearInterval(timer)
      reload(strategyCacheKey(id))
    }, CHECK_POLL_MS)
    return () => clearInterval(timer)
  }, [waiting, id])
  return strategy
}
