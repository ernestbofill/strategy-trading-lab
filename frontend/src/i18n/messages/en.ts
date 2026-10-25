import { common } from './en/common'
import { aiRound } from './en/aiRound'
import { account } from './en/account'
import { admin } from './en/admin'
import { dashboard } from './en/dashboard'
import { feedback } from './en/feedback'
import { newStrategy } from './en/newStrategy'
import { signal } from './en/signal'
import { strategy } from './en/strategy'
import { trades } from './en/trades'

/** Every English message. The other languages must have the same keys (the types check it). */
export const en = {
  ...common,
  ...aiRound,
  ...account,
  ...admin,
  ...dashboard,
  ...feedback,
  ...newStrategy,
  ...signal,
  ...strategy,
  ...trades,
}

export type MessageKey = keyof typeof en
