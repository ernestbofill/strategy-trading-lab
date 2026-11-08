// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type AdminDatabases, type AdminSetting } from '../../api'
import { AdminSettings } from './AdminSettings'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

const databases: AdminDatabases = {
  active: 'dev',
  home: 'dev',
  databases: [
    { name: 'dev', variable: 'DATABASE_URL', host: 'dev.neon.tech' },
    { name: 'production', variable: 'DATABASE_URL_PRODUCTION', host: 'prod.neon.tech' },
  ],
}

const settings: AdminSetting[] = [
  {
    key: 'SIGN_UPS',
    value: 'open',
    default: 'open',
    source: 'page',
    group: 'Access',
    description: 'Who can register',
    isSet: true,
    secret: false,
  },
]

describe('AdminSettings', () => {
  it('renders management nav rows including feedback link, and does not render the feedback section', async () => {
    vi.spyOn(api, 'getAdminSettings').mockResolvedValue(settings)
    vi.spyOn(api, 'getAdminDatabases').mockResolvedValue(databases)
    vi.spyOn(api, 'getVersion').mockResolvedValue({ database: 'dev', version: '1.0.0', commit: 'abcdef1', builtAt: null })
    const feedbackSpy = vi.spyOn(api, 'getFeedback')

    render(
      <MemoryRouter>
        <AdminSettings />
      </MemoryRouter>,
    )

    // Management nav rows
    expect(await screen.findByRole('link', { name: /Users/i })).toBeTruthy()
    expect(screen.getByRole('link', { name: /Invites/i })).toBeTruthy()
    expect(screen.getByRole('link', { name: /Assets/i })).toBeTruthy()

    const feedbackLink = screen.getByRole('link', { name: /Feedback/i })
    expect(feedbackLink.getAttribute('href')).toBe('/feedback')

    // FeedbackSection items should not be present
    expect(screen.queryByRole('button', { name: /Copy open as Markdown/i })).toBeNull()
    expect(screen.queryByLabelText(/New comment/i)).toBeNull()
    expect(feedbackSpy).not.toHaveBeenCalled()
  })
})
