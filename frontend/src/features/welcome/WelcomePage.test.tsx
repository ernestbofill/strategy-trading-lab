// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { I18nProvider } from '../../i18n/I18n'
import { WelcomePage } from './WelcomePage'

function renderPage() {
  return render(
    <MemoryRouter>
      <I18nProvider>
        <WelcomePage />
      </I18nProvider>
    </MemoryRouter>
  )
}

afterEach(() => {
  cleanup()
})

describe('WelcomePage', () => {
  it('renders hero title, lead, and guest/account actions', () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Trade rules, not emotions.' })).toBeTruthy()
    expect(screen.getByText(/A transparent strategy lab/)).toBeTruthy()

    const guestLink = screen.getAllByRole('link', { name: 'TRY AS GUEST ›' })[0]
    expect(guestLink.getAttribute('href')).toBe('/new')

    const registerLink = screen.getAllByRole('link', { name: 'Create an account' })[0]
    expect(registerLink.getAttribute('href')).toBe('/sign-up')

    const loginLink = screen.getByRole('link', { name: 'Log in' })
    expect(loginLink.getAttribute('href')).toBe('/log-in')
  })

  it('renders all 3 core features: strategy builder, simulation, and trading tracker', () => {
    renderPage()

    expect(screen.getByRole('tab', { name: /Strategy builder/ })).toBeTruthy()
    expect(screen.getByRole('tab', { name: /Real-time simulation/ })).toBeTruthy()
    expect(screen.getByRole('tab', { name: /Trading tracker/ })).toBeTruthy()
  })

  it('interactively switches template details inside strategy builder', () => {
    renderPage()

    // Builder tab is active by default
    expect(screen.getByText('SMA 200 + 5%–15% discount zone')).toBeTruthy()

    // Click "The comeback" template pill
    fireEvent.click(screen.getByRole('button', { name: 'The comeback' }))
    expect(screen.getByText('Breakout above 50-day moving average')).toBeTruthy()

    // Click "Bear markets" template pill
    fireEvent.click(screen.getByRole('button', { name: 'Bear markets' }))
    expect(screen.getByText(/S&P 500 drop/)).toBeTruthy()
  })

  it('expands simulation feature and toggles sub-tabs', () => {
    renderPage()

    // Switch to simulation tab
    fireEvent.click(screen.getByRole('tab', { name: /Real-time simulation/ }))

    expect(screen.getByText('+14.2%')).toBeTruthy()
    expect(screen.getByText('+8.1%')).toBeTruthy()
    expect(screen.getByText('-12.4%')).toBeTruthy()

    // Toggle sub-tab to paper trades log
    fireEvent.click(screen.getByRole('button', { name: 'Paper trade log' }))
    expect(screen.getByText(/Bought 1 step · AAPL/)).toBeTruthy()
    expect(screen.getByText(/Bought 1 step · MSFT/)).toBeTruthy()
  })

  it('expands trading tracker and provides interactive signal execution preview', () => {
    renderPage()

    // Switch to trading tracker tab
    fireEvent.click(screen.getByRole('tab', { name: /Trading tracker/ }))

    expect(screen.getByText('Zscaler (ZS)')).toBeTruthy()
    expect(screen.getByText(/Dipped 8.2% below 52-week peak/)).toBeTruthy()

    // Click "I did it"
    fireEvent.click(screen.getByRole('button', { name: 'I did it' }))
    expect(screen.getByRole('status').textContent).toMatch(/Trade recorded/)

    // Click "I skipped it"
    fireEvent.click(screen.getByRole('button', { name: 'I skipped it' }))
    expect(screen.getByRole('status').textContent).toMatch(/Step skipped/)

    // Click "Remind me tomorrow"
    fireEvent.click(screen.getByRole('button', { name: 'Remind me tomorrow' }))
    expect(screen.getByRole('status').textContent).toMatch(/Snoozed/)
  })

  it('renders trust pillars, bottom call to action, and full legal disclaimer', () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 2, name: 'Built for transparency and control' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 3, name: 'Zero broker access' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 3, name: 'Uncompromising benchmark honesty' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 3, name: 'Disciplined systematic rules' })).toBeTruthy()

    expect(screen.getByRole('heading', { level: 2, name: 'Ready to test your first strategy?' })).toBeTruthy()
    expect(screen.getByText(/you decide and trade at your own broker/)).toBeTruthy()
  })
})
