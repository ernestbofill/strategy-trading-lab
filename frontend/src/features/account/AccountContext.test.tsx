// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type Account } from '../../api'
import { AccountProvider, useAccount } from './AccountContext'

const account = (email: string): Account => ({ email, emailVerified: true, locale: 'en', timeZone: 'UTC', overviewEmail: true, currency: 'EUR', role: 'user', aiSharing: false })

/** A promise the test settles by hand. */
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((r) => (resolve = r))
  return { promise, resolve }
}

afterEach(() => vi.restoreAllMocks())

describe('AccountProvider', () => {
  it("doesn't let an older check undo a newer log-in", async () => {
    const startUp = deferred<Account | null>()
    const afterLogIn = deferred<Account | null>()
    vi.spyOn(api, 'getAccount').mockReturnValueOnce(startUp.promise).mockReturnValueOnce(afterLogIn.promise)
    const { result } = renderHook(() => useAccount(), { wrapper: AccountProvider })

    let refreshed: Promise<unknown>
    act(() => void (refreshed = result.current.refresh()))
    await act(async () => {
      afterLogIn.resolve(account('a@example.com'))
      await refreshed
    })
    await act(async () => startUp.resolve(null)) // The start-up check, sent before the log-in, answers last.

    expect(result.current.state).toEqual({ status: 'ready', account: account('a@example.com') })
    expect(result.current.owner).toBe('a@example.com')
  })

  it('keeps the owner through a log-out and changes it for another user', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValueOnce(account('a@example.com')).mockResolvedValueOnce(account('b@example.com'))
    const { result } = renderHook(() => useAccount(), { wrapper: AccountProvider })
    await waitFor(() => expect(result.current.owner).toBe('a@example.com'))

    act(() => result.current.clear())
    expect(result.current.state).toEqual({ status: 'ready', account: null })
    expect(result.current.owner).toBe('a@example.com')

    await act(async () => void (await result.current.refresh()))
    expect(result.current.owner).toBe('b@example.com')
  })

  it('keeps a known account when a re-check fails, and says it couldn\'t tell', async () => {
    vi.spyOn(api, 'getAccount').mockResolvedValueOnce(account('a@example.com')).mockRejectedValueOnce(new Error('offline'))
    const { result } = renderHook(() => useAccount(), { wrapper: AccountProvider })
    await waitFor(() => expect(result.current.state.status).toBe('ready'))

    let answer: unknown = null
    await act(async () => void (answer = await result.current.refresh()))
    expect(answer).toBeUndefined()
    expect(result.current.state).toEqual({ status: 'ready', account: account('a@example.com') })
  })

  it('answers an overtaken refresh with the latest result, the one the state shows', async () => {
    const logIn = deferred<Account | null>()
    const recheck = deferred<Account | null>()
    vi.spyOn(api, 'getAccount')
      .mockResolvedValueOnce(null)
      .mockReturnValueOnce(logIn.promise)
      .mockReturnValueOnce(recheck.promise)
    const { result } = renderHook(() => useAccount(), { wrapper: AccountProvider })
    await waitFor(() => expect(result.current.state.status).toBe('ready'))

    let first: Promise<unknown> = Promise.resolve()
    act(() => {
      first = result.current.refresh() // After the log-in.
      void result.current.refresh() // The tab came back into view meanwhile.
    })
    await act(async () => {
      logIn.resolve(account('a@example.com'))
      recheck.resolve(null)
    })

    await expect(first).resolves.toBeNull()
    expect(result.current.state).toEqual({ status: 'ready', account: null })
  })
})
