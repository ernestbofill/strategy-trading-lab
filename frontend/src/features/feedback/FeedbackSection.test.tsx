// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type FeedbackItem } from '../../api'
import { FeedbackSection } from './FeedbackSection'

const makeItem = (overrides: Partial<FeedbackItem> = {}): FeedbackItem => ({
  id: 'f1',
  userEmail: 'reader@example.com',
  kind: 'bug',
  status: 'open',
  text: 'Button overlaps the footer on narrow screens',
  route: '/dashboard',
  element: 'main > button',
  selectedText: 'Start a strategy',
  closedAt: null,
  createdAt: '2026-10-25T10:00:00Z',
  updatedAt: '2026-10-25T10:00:00Z',
  ...overrides,
})

afterEach(() => {
  vi.restoreAllMocks()
  cleanup()
})

describe('FeedbackSection', () => {
  it('lists open items, filters closed/all, and lets the owner edit, close/reopen, delete, and copy Markdown', async () => {
    const openItem = makeItem()
    const closedItem = makeItem({
      id: 'f2',
      userEmail: 'vip@example.com',
      kind: 'feature',
      status: 'closed',
      text: 'Dark mode contrast fixed',
      closedAt: '2026-10-25T11:00:00Z',
    })

    vi.spyOn(api, 'getFeedback').mockResolvedValue([openItem, closedItem])
    const updateSpy = vi.spyOn(api, 'updateFeedback').mockImplementation(async (id, patch) => ({
      ...(id === 'f1' ? openItem : closedItem),
      ...(patch.kind ? { kind: patch.kind } : {}),
      ...(patch.text ? { text: patch.text } : {}),
      ...(patch.status ? { status: patch.status, closedAt: patch.status === 'closed' ? '2026-10-25T12:00:00Z' : null } : {}),
    }))
    const deleteSpy = vi.spyOn(api, 'deleteFeedback').mockResolvedValue()
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.assign(navigator, { clipboard: { writeText } })

    render(
      <MemoryRouter>
        <FeedbackSection />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Button overlaps the footer on narrow screens')).toBeTruthy()
    expect(screen.getByText('reader@example.com')).toBeTruthy()
    expect(screen.queryByText('Dark mode contrast fixed')).toBeNull()

    // Copy open as Markdown
    fireEvent.click(screen.getByRole('button', { name: /Copy open as Markdown \(1\)/i }))
    await waitFor(() => expect(writeText).toHaveBeenCalledOnce())
    expect(writeText.mock.calls[0][0]).toContain('reader@example.com')
    expect(writeText.mock.calls[0][0]).toContain('Button overlaps the footer on narrow screens')

    // Edit comment
    fireEvent.click(screen.getByRole('button', { name: 'Edit' }))
    fireEvent.change(screen.getByLabelText('Edit comment'), {
      target: { value: 'Updated bug description' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() =>
      expect(updateSpy).toHaveBeenCalledWith('f1', { kind: 'bug', text: 'Updated bug description' }),
    )
    expect(await screen.findByText('Updated bug description')).toBeTruthy()

    // Close item
    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    await waitFor(() => expect(updateSpy).toHaveBeenCalledWith('f1', { status: 'closed' }))

    // Switch to Closed tab to see both closed items
    fireEvent.click(screen.getByRole('button', { name: /Closed \(2\)/i }))
    expect(screen.getByText('Dark mode contrast fixed')).toBeTruthy()

    // Delete the second item
    const deleteButtons = screen.getAllByRole('button', { name: 'Delete' })
    fireEvent.click(deleteButtons[1])
    await waitFor(() => expect(deleteSpy).toHaveBeenCalledWith('f2'))
  })

  it('lets the owner add a general feedback item without selected text', async () => {
    vi.spyOn(api, 'getFeedback').mockResolvedValue([])
    const submitSpy = vi.spyOn(api, 'submitFeedback').mockResolvedValue(
      makeItem({
        id: 'f3',
        userEmail: 'owner@example.com',
        kind: 'idea',
        text: 'General note from admin page',
        route: null,
        element: null,
        selectedText: null,
      }),
    )

    render(
      <MemoryRouter>
        <FeedbackSection />
      </MemoryRouter>,
    )

    await screen.findByText(/No open feedback items yet/i)

    fireEvent.click(screen.getByRole('radio', { name: /Idea/i }))
    fireEvent.change(screen.getByLabelText('New comment'), {
      target: { value: 'General note from admin page' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Add comment' }))

    await waitFor(() =>
      expect(submitSpy).toHaveBeenCalledWith({
        kind: 'idea',
        text: 'General note from admin page',
        route: null,
        element: null,
        selectedText: null,
      }),
    )
    expect(await screen.findByText('General note from admin page')).toBeTruthy()
  })

  it('renders title as a link when titleLink is provided', async () => {
    vi.spyOn(api, 'getFeedback').mockResolvedValue([])

    render(
      <MemoryRouter>
        <FeedbackSection titleLink="/feedback" />
      </MemoryRouter>,
    )

    const link = await screen.findByRole('link', { name: 'Feedback' })
    expect(link.getAttribute('href')).toBe('/feedback')
  })
})
