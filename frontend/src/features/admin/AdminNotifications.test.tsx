// @vitest-environment jsdom
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { api, type AdminNotifications as Page, type AdminPush } from '../../api'
import { AdminNotifications } from './AdminNotifications'

const sent: AdminPush = {
  id: 'n1', userId: 'u1', email: 'anna@example.com', kind: 'test', title: 'Hello', body: 'A test',
  createdAt: '2026-11-24T10:00:00Z', sentAt: '2026-11-24T10:00:01Z', failedAt: null, error: null,
}
const page: Page = { recipients: [{ id: 'u1', email: 'anna@example.com', devices: 2 }], pushes: [] }

describe('AdminNotifications', () => {
  it('sends a test push to the chosen user and lists it', async () => {
    vi.spyOn(api, 'getAdminNotifications').mockResolvedValueOnce(page).mockResolvedValue({ ...page, pushes: [sent] })
    const send = vi.spyOn(api, 'sendAdminTestPush').mockResolvedValue(sent)
    render(
      <MemoryRouter>
        <AdminNotifications />
      </MemoryRouter>,
    )

    expect(await screen.findByRole('option', { name: 'anna@example.com (2 devices)' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Send test push' }))
    expect(await screen.findByText('Write a title.')).toBeTruthy()
    expect(send).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Hello' } })
    fireEvent.change(screen.getByLabelText('Text'), { target: { value: 'A test' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send test push' }))

    expect(await screen.findByText('Sent to anna@example.com.')).toBeTruthy()
    expect(send).toHaveBeenCalledWith({ userId: 'u1', title: 'Hello', body: 'A test' })
    expect(screen.getAllByText('A test').length).toBeGreaterThan(0)
  })
})
