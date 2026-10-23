// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { invalidateAll, useLoadable } from './useLoadable'

describe('useLoadable', () => {
  it('loads again on a screen that is open when every result is forgotten (another user)', async () => {
    let user = 'a'
    const { result } = renderHook(() => useLoadable('test:dashboard', async () => `${user}'s dashboard`))
    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: "a's dashboard" }))

    user = 'b'
    act(() => invalidateAll())

    await waitFor(() => expect(result.current).toEqual({ status: 'ready', data: "b's dashboard" }))
  })
})
