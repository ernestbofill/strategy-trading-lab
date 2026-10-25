import { common } from './ca/common'
import { account } from './ca/account'
import { admin } from './ca/admin'
import { dashboard } from './ca/dashboard'
import { newStrategy } from './ca/newStrategy'
import { signal } from './ca/signal'
import { strategy } from './ca/strategy'

import type { MessageKey } from './en'

export const ca: Record<MessageKey, string> = {
  ...common,
  ...account,
  ...admin,
  ...dashboard,
  ...newStrategy,
  ...signal,
  ...strategy,
}
