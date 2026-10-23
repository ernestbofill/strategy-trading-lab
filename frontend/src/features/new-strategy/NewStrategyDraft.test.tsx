// @vitest-environment jsdom
import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'
import { NewStrategyDraftProvider, useNewStrategyDraft } from './NewStrategyDraft'

describe('NewStrategyDraftProvider', () => {
  it('keeps the draft for the same user, through a log-out, and starts over for another', () => {
    let owner: string | null = 'a@example.com'
    const wrapper = ({ children }: { children: ReactNode }) => <NewStrategyDraftProvider owner={owner}>{children}</NewStrategyDraftProvider>
    const { result, rerender } = renderHook(() => useNewStrategyDraft(), { wrapper })

    act(() => result.current.update({ budget: 5000, mode: 'live' }))
    owner = null // Logged out.
    rerender()
    owner = 'a@example.com' // The same user, back after the session ended.
    rerender()
    expect(result.current).toMatchObject({ budget: 5000, mode: 'live' })

    owner = 'b@example.com'
    rerender()
    expect(result.current).toMatchObject({ budget: null, mode: 'simulation' })
  })
})
