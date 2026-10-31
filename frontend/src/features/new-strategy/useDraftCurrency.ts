import { useEffect } from 'react'
import type { Currency } from '../../api'
import { useOptionalAccount } from '../account/AccountContext'
import { useNewStrategyDraft } from './NewStrategyDraft'

/**
 * The currency the amounts are shown in. A live copy keeps its simulation's; otherwise it is the account's. A guest
 * has none yet, so the draft pins EUR: signing up in another currency doesn't relabel the amounts they entered.
 */
export function useDraftCurrency(): Currency {
  const { currency, update } = useNewStrategyDraft()
  const account = useOptionalAccount()
  const isGuest = !account
  useEffect(() => {
    if (isGuest && currency === null) update({ currency: 'EUR' })
  }, [isGuest, currency, update])
  return currency ?? account?.currency ?? 'EUR'
}
