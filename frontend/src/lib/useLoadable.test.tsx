// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { invalidate, invalidateAll, useLoadable } from './useLoadable'

describe('useLoadable', () => {
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
})
