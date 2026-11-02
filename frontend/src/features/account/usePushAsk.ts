import { useState } from 'react'
import { api } from '../../api'
import { notePushAsked, pushAsked, pushSupport, subscribeDevice } from '../../lib/push'
import { useAccount, useOptionalAccount } from './AccountContext'
import { usePushOffered } from './usePushOffered'

/**
 * Confirm's "Allow notifications on this device" (spec: Notifications, Ernest 2026-11-02), for every strategy:
 * - `ask`: the browser hasn't been asked; a checkbox, ticked the first time on this device, unticked once it was put off;
 * - `granted`: already allowed, so no checkbox: starting subscribes the device quietly;
 * - `blocked` / `homeScreen`: a line saying why it can't ask;
 * - `none`: nothing (a guest, no push on this server or browser).
 */
export type PushAskKind = 'ask' | 'granted' | 'blocked' | 'homeScreen' | 'none'

export function usePushAsk() {
  const account = useOptionalAccount()
  const { refresh } = useAccount()
  const offered = usePushOffered()
  const support = pushSupport()
  const kind: PushAskKind = !account || !offered
    ? 'none'
    : support === 'ready'
      ? Notification.permission === 'granted' ? 'granted' : 'ask'
      : support === 'blocked' || support === 'homeScreen' ? support : 'none'
  const [ticked, setTicked] = useState(() => !pushAsked())

  /**
   * Call it first thing in the Start tap, before anything else is awaited: the browser only asks from a tap. Never throws, so
   * the strategy starts whatever the answer. Ticking it also turns the account's App box back on.
   */
  const run = async () => {
    try {
      if (kind === 'granted') {
        await subscribeDevice()
      } else if (kind === 'ask') {
        notePushAsked()
        if (ticked && (await subscribeDevice()) && account && !account.alertPush) {
          await api.setAlertPush(true)
          await refresh()
        }
      }
    } catch {
      // Best effort: Account → Notifications shows the device's state.
    }
  }

  return { kind, ticked, setTicked, run }
}
