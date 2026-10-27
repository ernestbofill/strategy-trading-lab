// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api, TooManyTriesError, type FeedbackItem } from '../../api'
import { SendFeedback } from './SendFeedback'

let emailVerified = true
vi.mock('../account/AccountContext', () => ({
  useCurrentAccount: () => ({ emailVerified }),
}))

const saved: FeedbackItem = {
  id: 'f1',
  userEmail: 'reader@example.com',
  kind: 'bug',
  status: 'open',
  text: 'The chart is slow',
  route: null,
  element: null,
  selectedText: null,
  closedAt: null,
  createdAt: '2026-10-27T10:00:00Z',
  updatedAt: '2026-10-27T10:00:00Z',
}

const renderForm = () =>
  render(
    <MemoryRouter initialEntries={['/account']}>
      <SendFeedback />
    </MemoryRouter>,
  )

describe('SendFeedback', () => {
  beforeEach(() => {
    emailVerified = true
  })

  afterEach(() => {
    vi.restoreAllMocks()
    cleanup()
  })

  it('sends a comment with its type, not tied to any page, and clears the box', async () => {
    const submit = vi.spyOn(api, 'submitFeedback').mockResolvedValue(saved)
    renderForm()

    const send = screen.getByRole('button', { name: 'Send feedback' }) as HTMLButtonElement
    expect(send.disabled).toBe(true)

    fireEvent.click(screen.getByRole('radio', { name: /Bug/ }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Your comment' }), { target: { value: '  The chart is slow  ' } })
    fireEvent.click(send)

    expect(await screen.findByText('Thanks, it’s sent. We may answer by email.')).toBeTruthy()
    expect(submit).toHaveBeenCalledWith({ kind: 'bug', text: 'The chart is slow' })
    expect((screen.getByRole('textbox', { name: 'Your comment' }) as HTMLTextAreaElement).value).toBe('')
  })

  it('says why it was not sent', async () => {
    vi.spyOn(api, 'submitFeedback').mockRejectedValue(new TooManyTriesError())
    renderForm()

    fireEvent.change(screen.getByRole('textbox', { name: 'Your comment' }), { target: { value: 'Again' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send feedback' }))

    expect(await screen.findByText('Too many comments sent recently. Try again later.')).toBeTruthy()
  })

  it('asks for a confirmed email first', () => {
    emailVerified = false
    renderForm()

    expect(screen.getByText('Confirm your email to send feedback.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Send feedback' })).toBeNull()
  })
})
