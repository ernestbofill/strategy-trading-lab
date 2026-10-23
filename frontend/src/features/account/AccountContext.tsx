import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { api, onSessionEnded, type Account } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { rememberLoggedIn } from './knownDevice'

type AccountState = { status: 'loading' } | { status: 'error' } | { status: 'ready'; account: Account | null }

interface AccountContextValue {
  state: AccountState
  /**
   * Reads the account again (after log-in, sign-up, a reset or a verified email): the account, null without a
   * session, or undefined when it couldn't tell (the request failed).
   */
  refresh: () => Promise<Account | null | undefined>
  /** The last user logged in on this tab, kept through a log-out: a new one starts the new-strategy draft over. */
  owner: string | null
  /** The session is gone (log-out, or a 401). */
  clear: () => void
}

const AccountContext = createContext<AccountContextValue | null>(null)

/**
 * Who is logged in: asked at app start (`/account/me`) and whenever the tab comes back into view (another tab shares
 * the cookie and may have logged in someone else), then kept in step with log-in, log-out and 401s.
 */
export function AccountProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AccountState>({ status: 'loading' })
  // The cached loads and the new-strategy draft belong to the owner: a different one clears both. A ref mirrors it,
  // so a refresh compares against the latest owner, not the one of its render.
  const [owner, setOwner] = useState<string | null>(null)
  const ownerRef = useRef<string | null>(null)
  /**
   * Bumped by every refresh and clear: only the latest one's answer counts, so an older /me can't undo a log-in. An
   * overtaken refresh answers with the latest one's result, so its caller acts on what the state shows.
   */
  const latest = useRef(0)
  const latestResult = useRef<Promise<Account | null | undefined>>(Promise.resolve(null))

  const check = useCallback(async (mine: number): Promise<Account | null | undefined> => {
    try {
      const account = await api.getAccount()
      if (mine !== latest.current) return latestResult.current
      if (!account) invalidateAll()
      else {
        rememberLoggedIn()
        if (account.email !== ownerRef.current) {
          invalidateAll()
          ownerRef.current = account.email
          setOwner(account.email)
        }
      }
      setState({ status: 'ready', account })
      return account
    } catch {
      if (mine !== latest.current) return latestResult.current
      // A failed re-check keeps what is known; only a first check that fails shows the error.
      setState((s) => (s.status === 'ready' ? s : { status: 'error' }))
      return undefined
    }
  }, [])

  const refresh = useCallback(() => {
    const result = check(++latest.current)
    latestResult.current = result
    return result
  }, [check])

  const clear = useCallback(() => {
    latest.current++
    latestResult.current = Promise.resolve(null)
    invalidateAll()
    setState({ status: 'ready', account: null })
  }, [])

  useEffect(() => {
    // The start-up check: its state is set after the request, not synchronously.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refresh()
    const recheck = () => document.visibilityState === 'visible' && void refresh()
    document.addEventListener('visibilitychange', recheck)
    const stop = onSessionEnded(clear)
    return () => {
      document.removeEventListener('visibilitychange', recheck)
      stop()
    }
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
