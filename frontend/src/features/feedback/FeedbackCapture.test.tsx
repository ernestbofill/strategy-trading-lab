// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../api'
import { FeedbackCapture } from './FeedbackCapture'

let emailVerified = true
vi.mock('../account/AccountContext', () => ({
  useCurrentAccount: () => ({ emailVerified }),
}))

function selectElementText(el: Element) {
  const range = document.createRange()
  range.selectNodeContents(el)
  const sel = window.getSelection()!
  sel.removeAllRanges()
  sel.addRange(range)
  document.dispatchEvent(new Event('selectionchange'))
}

describe('FeedbackCapture', () => {
  beforeEach(() => {
    emailVerified = true
    vi.useFakeTimers()
  })

  afterEach(() => {
    window.getSelection()?.removeAllRanges()
    vi.useRealTimers()
    vi.restoreAllMocks()
    cleanup()
  })

  it('lets any user with a confirmed email select text and submit feedback', async () => {
    const submitSpy = vi.spyOn(api, 'submitFeedback').mockResolvedValue({
      id: 'f1',
      userEmail: 'reader@example.com',
      kind: 'bug',
      status: 'open',
      text: 'This price looks off',
      route: '/strategies/s1',
      element: 'main > p#target',
      selectedText: 'Last close $142.10',
      closedAt: null,
      createdAt: '2026-10-25T10:00:00Z',
      updatedAt: '2026-10-25T10:00:00Z',
    })

    render(
      <MemoryRouter initialEntries={['/strategies/s1']}>
        <main>
          <p id="target">Last close $142.10</p>
        </main>
        <FeedbackCapture />
      </MemoryRouter>,
    )

    selectElementText(document.getElementById('target')!)
    act(() => {
      vi.advanceTimersByTime(150)
    })
    vi.useRealTimers()

    const addBtn = screen.getByRole('button', { name: /Add comment/i })
    fireEvent.click(addBtn)

    expect(screen.getByRole('heading', { name: 'Add a comment' })).toBeTruthy()
    expect(screen.getByText('“Last close $142.10”')).toBeTruthy()

    fireEvent.click(screen.getByRole('radio', { name: /Bug/i }))
    fireEvent.change(screen.getByLabelText('Your comment'), {
      target: { value: '  This price looks off  ' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(submitSpy).toHaveBeenCalledOnce())
    expect(submitSpy).toHaveBeenCalledWith({
      kind: 'bug',
      text: 'This price looks off',
      route: '/strategies/s1',
      element: 'p#target',
      selectedText: 'Last close $142.10',
    })
    expect((await screen.findByRole('status')).textContent).toContain('Comment saved')
  })

  it('does not show the feedback button when the user has not confirmed their email', () => {
    emailVerified = false

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <main>
          <p id="target">Some strategy text</p>
        </main>
        <FeedbackCapture />
      </MemoryRouter>,
    )

    selectElementText(document.getElementById('target')!)
    act(() => {
      vi.advanceTimersByTime(150)
    })

    expect(screen.queryByRole('button', { name: /Add comment/i })).toBeNull()
  })

  it('ignores selections inside inputs and textareas', () => {
    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <main>
          <textarea id="notes" defaultValue="Selected inside textarea" />
        </main>
        <FeedbackCapture />
      </MemoryRouter>,
    )

    selectElementText(document.getElementById('notes')!)
    act(() => {
      vi.advanceTimersByTime(150)
    })

    expect(screen.queryByRole('button', { name: /Add comment/i })).toBeNull()
  })
})
