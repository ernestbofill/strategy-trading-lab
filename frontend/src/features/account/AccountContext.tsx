import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { api, onSessionEnded, type Account } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { rememberLoggedIn } from './knownDevice'

type AccountState = { status: 'loading' } | { status: 'error' } | { status: 'ready'; account: Account | null }

interface AccountContextValue {
  state: AccountState
  /** Reads the account again (after log-in, sign-up, a reset or a verified email). */
  refresh: () => Promise<Account | null>
  /** The last user logged in on this tab, kept through a log-out: a new one starts the new-strategy draft over. */
  owner: string | null
  /** The session is gone (log-out, or a 401). */
  clear: () => void
}

const AccountContext = createContext<AccountContextValue | null>(null)

/** Who is logged in: asked once at app start (`/account/me`), then kept in step with log-in, log-out and 401s. */
export function AccountProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AccountState>({ status: 'loading' })
  const [owner, setOwner] = useState<string | null>(null)
  /** Bumped by every refresh and clear: only the latest one's answer counts, so an older /me can't undo a log-in. */
  const latest = useRef(0)

  /** Whose data the caches hold: a different user (or none) means nothing loaded may show. */
  const cachedFor = useRef<string | null>(null)
  const switchUser = useCallback((email: string | null) => {
    if (email !== cachedFor.current) invalidateAll()
    cachedFor.current = email
  }, [])

  const refresh = useCallback(async () => {
    const mine = ++latest.current
    try {
      const account = await api.getAccount()
      if (mine !== latest.current) return account
      switchUser(account?.email ?? null)
      if (account) {
        rememberLoggedIn()
        setOwner(account.email)
      }
      setState({ status: 'ready', account })
      return account
    } catch {
      if (mine === latest.current) setState({ status: 'error' })
      return null
    }
  }, [switchUser])

  const clear = useCallback(() => {
    latest.current++
    switchUser(null)
    setState({ status: 'ready', account: null })
  }, [switchUser])

  useEffect(() => {
    // The start-up check: its state is set after the request, not synchronously.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refresh()
    return onSessionEnded(clear)
  }, [refresh, clear])

  const value = useMemo(() => ({ state, refresh, owner, clear }), [state, refresh, owner, clear])
  return <AccountContext.Provider value={value}>{children}</AccountContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAccount(): AccountContextValue {
  const value = useContext(AccountContext)
  if (!value) throw new Error('useAccount needs an AccountProvider.')
  return value
}

/** The logged-in user, inside `RequireAccount`. */
// eslint-disable-next-line react-refresh/only-export-components
export function useCurrentAccount(): Account {
  const { state } = useAccount()
  if (state.status !== 'ready' || !state.account) throw new Error('useCurrentAccount needs RequireAccount.')
  return state.account
}
