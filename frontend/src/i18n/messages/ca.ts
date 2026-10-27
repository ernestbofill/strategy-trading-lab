import { common } from './ca/common'
import { aiRound } from './ca/aiRound'
import { account } from './ca/account'
import { admin } from './ca/admin'
import { dashboard } from './ca/dashboard'
import { feedback } from './ca/feedback'
import { newStrategy } from './ca/newStrategy'
import { signal } from './ca/signal'
import { strategy } from './ca/strategy'
import { trades } from './ca/trades'

import type { MessageKey } from './en'

export const ca: Record<MessageKey, string> = {
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
