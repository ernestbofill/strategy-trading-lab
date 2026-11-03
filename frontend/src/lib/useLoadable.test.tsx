// @vitest-environment jsdom
import { act, cleanup, renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { invalidate, invalidateAll, refreshAll, useLoadable } from './useLoadable'

describe('useLoadable', () => {
  afterEach(() => {
    cleanup()
    vi.restoreAllMocks()
  })

  it('loads again on a screen that is open when every result is forgotten (another user)', async () => {
    let user = 'a'
    const { result } = renderHook(() => useLoadable('test:dashboard', async () => `${user}'s dashboard`))
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: "a's dashboard" }))

    user = 'b'
    act(() => invalidateAll())

    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: "b's dashboard" }))
  })

  it('loads again on a screen that is open when its cache key is invalidated', async () => {
    let count = 1
    const { result } = renderHook(() => useLoadable('test:strategy:1', async () => `trade count: ${count}`))
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'trade count: 1' }))

    count = 2
    act(() => invalidate('test:strategy:1'))

    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'trade count: 2' }))
  })

  it('does not reload when a different cache key is invalidated', async () => {
    let count = 1
    const { result } = renderHook(() => useLoadable('test:strategy:2', async () => `trade count: ${count}`))
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'trade count: 1' }))

    count = 2
    act(() => invalidate('test:strategy:other'))

    expect(result.current).toEqual({ status: 'ready', data: 'trade count: 1' })
  })

  it('loads fresh data on next mount when invalidated while not mounted', async () => {
    let count = 1
    const { result, unmount } = renderHook(() => useLoadable('test:strategy:3', async () => `count: ${count}`))
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'count: 1' }))
    unmount()

    count = 2
    act(() => invalidate('test:strategy:3'))

    const { result: remounted } = renderHook(() => useLoadable('test:strategy:3', async () => `count: ${count}`))
    await waitFor(() => expect(remounted.current).toEqual({ status: 'ready', data: 'count: 2' }))
  })

  it('shows the reloaded result on a screen that opens while the reload is in flight', async () => {
    // Confirm shows the dashboard, invalidates it and navigates there: Confirm starts the reload, then the dashboard
    // opens before it finishes and must not keep the old list.
    let count = 1
    let finish = () => {}
    const load = () =>
      count === 1 ? Promise.resolve('count: 1') : new Promise<string>((resolve) => (finish = () => resolve('count: 2')))
    const first = renderHook(() => useLoadable('test:strategy:4', load))
    await waitFor(() => expect(first.result.current).toEqual({ status: 'ready', data: 'count: 1' }))

    count = 2
    act(() => invalidate('test:strategy:4'))
    first.unmount()
    const { result } = renderHook(() => useLoadable('test:strategy:4', load))
    expect(result.current).toEqual({ status: 'loading' })
    act(() => finish())

    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'count: 2' }))
  })

  it('shows the kept result on a screen that opens again, then what the server has now (a new signal)', async () => {
    let waiting = 2
    const load = async () => `waiting: ${waiting}`
    const first = renderHook(() => useLoadable('test:dashboard:2', load))
    await waitFor(() => expect(first.result.current).toEqual({ status: 'ready', data: 'waiting: 2' }))
    first.unmount()

    waiting = 3
    vi.spyOn(Date, 'now').mockReturnValue(Date.now() + 60_000)
    const { result } = renderHook(() => useLoadable('test:dashboard:2', load))
    expect(result.current).toEqual({ status: 'ready', data: 'waiting: 2' })
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'waiting: 3' }))
  })

  it('reloads an open screen on refreshAll and keeps its result when the reload fails', async () => {
    let waiting: number | null = 2
    let failures = 0
    const load = async () => {
      if (waiting === null) {
        failures++
        throw new Error('offline')
      }
      return `waiting: ${waiting}`
    }
    const { result } = renderHook(() => useLoadable('test:dashboard:3', load))
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'waiting: 2' }))

    waiting = 3
    act(() => refreshAll())
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'waiting: 3' }))

    waiting = null
    act(() => refreshAll())
    await waitFor(() => expect(failures).toBe(1))
    await act(async () => {})
    expect(result.current).toEqual({ status: 'ready', data: 'waiting: 3' })
  })

  it('shows the error when the reload after a change the user made fails, not the result from before it', async () => {
    let online = true
    const load = async () => {
      if (!online) throw new Error('offline')
      return 'trades: 2'
    }
    const { result } = renderHook(() => useLoadable('test:strategy:5', load))
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: 'trades: 2' }))

    online = false
    act(() => invalidate('test:strategy:5'))

    await waitFor(() => expect(result.current).toEqual({ status: 'error' }))
  })
})
