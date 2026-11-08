// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { I18nProvider } from '../../i18n/I18n'
import { WelcomePage } from './WelcomePage'

let mockRole: string | null = null

// A guest by default: the page shows the guest top bar (the language and LOG IN).
vi.mock('../account/AccountContext', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../account/AccountContext')>()),
  useRole: () => mockRole,
}))

function renderPage(locale: 'en' | 'ca' = 'en', initialEntries = ['/welcome']) {
  if (locale === 'ca') {
    localStorage.setItem('stl.locale', 'ca')
  } else {
    localStorage.removeItem('stl.locale')
  }
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <I18nProvider>
        <WelcomePage />
      </I18nProvider>
    </MemoryRouter>
  )
}

afterEach(() => {
  cleanup()
  localStorage.clear()
  mockRole = null
})

describe('WelcomePage', () => {
  it('renders hero title, lead, top bar login, and bottom CTA actions', () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Trade rules, not emotions.' })).toBeTruthy()
    expect(screen.getByText(/A transparent strategy lab/)).toBeTruthy()
    expect(screen.queryByText('SYSTEM READY // RULES-BASED LAB')).toBeNull()

    const guestLink = screen.getByRole('link', { name: 'TRY AS GUEST ›' })
    expect(guestLink.getAttribute('href')).toBe('/new')

    const registerLink = screen.getByRole('link', { name: 'Create an account' })
    expect(registerLink.getAttribute('href')).toBe('/sign-up')

    const loginLink = screen.getByRole('link', { name: 'LOG IN' })
    expect(loginLink.getAttribute('href')).toBe('/log-in')
  })

  it('renders all 3 core features with roving tabindex and supports collapsing/expanding', () => {
    renderPage()

    const builderTab = screen.getByRole('tab', { name: /Strategy builder/ })
    const simTab = screen.getByRole('tab', { name: /Real-time simulation/ })
    const trackerTab = screen.getByRole('tab', { name: /Trading tracker/ })

    // All three start collapsed; the first tab is the tabbable one
    expect(builderTab.getAttribute('aria-expanded')).toBe('false')
    expect(screen.queryByRole('tabpanel')).toBeNull()
    expect(builderTab.getAttribute('tabIndex')).toBe('0')
    expect(simTab.getAttribute('tabIndex')).toBe('-1')
    expect(trackerTab.getAttribute('tabIndex')).toBe('-1')

    // Clicking a tab expands its detail panel
    fireEvent.click(builderTab)
    expect(builderTab.getAttribute('aria-selected')).toBe('true')
    expect(screen.getByRole('tabpanel')).toBeTruthy()

    // Clicking the already-active tab collapses it again
    fireEvent.click(builderTab)
    expect(builderTab.getAttribute('aria-expanded')).toBe('false')
    expect(screen.queryByRole('tabpanel')).toBeNull()
  })

  it('supports arrow, Home, and End key navigation with focus management between feature pillar tabs', () => {
    renderPage()

    const builderTab = screen.getByRole('tab', { name: /Strategy builder/ })
    const simTab = screen.getByRole('tab', { name: /Real-time simulation/ })
    const trackerTab = screen.getByRole('tab', { name: /Trading tracker/ })

    fireEvent.keyDown(builderTab, { key: 'ArrowRight' })
    expect(simTab.getAttribute('aria-selected')).toBe('true')
    expect(simTab.getAttribute('tabIndex')).toBe('0')
    expect(builderTab.getAttribute('tabIndex')).toBe('-1')
    expect(document.activeElement).toBe(simTab)
    expect(screen.getByText('+14.2%')).toBeTruthy()

    fireEvent.keyDown(simTab, { key: 'End' })
    expect(trackerTab.getAttribute('aria-selected')).toBe('true')
    expect(document.activeElement).toBe(trackerTab)

    fireEvent.keyDown(trackerTab, { key: 'Home' })
    expect(builderTab.getAttribute('aria-selected')).toBe('true')
    expect(document.activeElement).toBe(builderTab)
  })

  it('replaces DASHBOARD with BACK on top bar when navigated with ?from=new', () => {
    mockRole = 'user'
    renderPage('en', ['/welcome?from=new'])

    const backLink = screen.getByRole('link', { name: 'BACK' })
    expect(backLink.getAttribute('href')).toBe('/new')
    expect(screen.queryByRole('link', { name: 'DASHBOARD' })).toBeNull()
    expect(screen.queryByRole('heading', { level: 2, name: 'Ready to test your first strategy?' })).toBeNull()
  })
})

describe('WelcomePage language switch', () => {
  it('switches to the other language and back', () => {
    renderPage()

    fireEvent.click(screen.getByRole('button', { name: 'Català' }))
    expect(localStorage.getItem('stl.locale')).toBe('ca')
    fireEvent.click(screen.getByRole('button', { name: 'English' }))
    expect(localStorage.getItem('stl.locale')).toBe('en')
  })
})
