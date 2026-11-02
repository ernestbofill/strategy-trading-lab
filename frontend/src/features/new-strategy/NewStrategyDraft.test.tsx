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

  describe('after a reload', () => {
    const mount = (getOwner: () => string | null | undefined) =>
      renderHook(() => useNewStrategyDraft(), {
        wrapper: ({ children }: { children: ReactNode }) => (
          <NewStrategyDraftProvider owner={getOwner()}>{children}</NewStrategyDraftProvider>
        ),
      })

    it('keeps the chosen template and choices', () => {
      const first = mount(() => null)
      act(() => first.result.current.switchTemplate('trend'))
      act(() => first.result.current.update({ budget: 4200, mode: 'live' }))
      first.unmount()

      const { result } = mount(() => null)
      expect(result.current).toMatchObject({ templateKey: 'trend', budget: 4200, mode: 'live' })
    })

    it('holds a user\'s draft while the account loads, then keeps it for the same user', () => {
      const first = mount(() => 'a@example.com')
      act(() => first.result.current.switchTemplate('ai'))
      first.unmount()

      let owner: string | null | undefined = undefined // The account is still loading.
      const { result, rerender } = mount(() => owner)
      expect(result.current.templateKey).toBe('ai')
      owner = 'a@example.com'
      rerender()
      expect(result.current.templateKey).toBe('ai')
    })

    it('starts over when someone else is logged in, or nobody', () => {
      const first = mount(() => 'a@example.com')
      act(() => first.result.current.switchTemplate('ai'))
      first.unmount()

      let owner: string | null | undefined = undefined
      const { result, rerender } = mount(() => owner)
      owner = null
      rerender()
      expect(result.current.templateKey).toBe('dip')
    })

    it('ignores a stored draft it cannot read', () => {
      sessionStorage.setItem('stl.newStrategyDraft.v1', '{not json')
      const { result } = mount(() => null)
      expect(result.current.templateKey).toBe('dip')
    })
  })
})
