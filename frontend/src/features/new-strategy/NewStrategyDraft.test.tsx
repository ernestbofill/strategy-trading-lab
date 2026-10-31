// @vitest-environment jsdom
import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'
import { NewStrategyDraftProvider, useNewStrategyDraft } from './NewStrategyDraft'

describe('NewStrategyDraftProvider', () => {
  it('starts over for another user and after a log-out', () => {
    let owner: string | null = 'a@example.com'
    const wrapper = ({ children }: { children: ReactNode }) => <NewStrategyDraftProvider owner={owner}>{children}</NewStrategyDraftProvider>
    const { result, rerender } = renderHook(() => useNewStrategyDraft(), { wrapper })

    act(() => result.current.update({ budget: 5000, mode: 'live' }))
    owner = 'b@example.com'
    rerender()
    expect(result.current).toMatchObject({ budget: null, mode: 'simulation' })

    act(() => result.current.update({ budget: 4000 }))
    owner = null // Logged out: the next guest must not see b's draft.
    rerender()
    expect(result.current).toMatchObject({ budget: null })

    act(() => result.current.update({ budget: 3000 }))
    owner = 'c@example.com' // The guest signs up: their draft stays.
    rerender()
    expect(result.current).toMatchObject({ budget: 3000 })
  })

  it('keeps the draft when a guest signs up or logs in', () => {
    let owner: string | null = null
    const wrapper = ({ children }: { children: ReactNode }) => <NewStrategyDraftProvider owner={owner}>{children}</NewStrategyDraftProvider>
    const { result, rerender } = renderHook(() => useNewStrategyDraft(), { wrapper })

    act(() => result.current.update({ budget: 3500, mode: 'live', name: 'My Guest Strategy' }))
    owner = 'newly-signed-up@example.com'
    rerender()

    expect(result.current).toMatchObject({ budget: 3500, mode: 'live', name: 'My Guest Strategy' })
  })
})
