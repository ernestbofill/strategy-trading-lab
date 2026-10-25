import { common } from './en/common'
import { account } from './en/account'
import { admin } from './en/admin'
import { dashboard } from './en/dashboard'
import { newStrategy } from './en/newStrategy'
import { signal } from './en/signal'
import { strategy } from './en/strategy'

/** Every English message. The other languages must have the same keys (the types check it). */
export const en = {
  ...common,
  ...account,
  ...admin,
  ...dashboard,
  ...newStrategy,
  ...signal,
  ...strategy,
}

export type MessageKey = keyof typeof en
