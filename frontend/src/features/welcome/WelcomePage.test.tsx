// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { I18nProvider } from '../../i18n/I18n'
import { WelcomePage } from './WelcomePage'

function renderPage(locale: 'en' | 'ca' = 'en') {
  if (locale === 'ca') {
    localStorage.setItem('stl.locale', 'ca')
  } else {
    localStorage.removeItem('stl.locale')
  }
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
  localStorage.clear()
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

  it('supports arrow key navigation between feature pillar tabs', () => {
    renderPage()

    const builderTab = screen.getByRole('tab', { name: /Strategy builder/ })
    const simTab = screen.getByRole('tab', { name: /Real-time simulation/ })

    expect(builderTab.getAttribute('aria-selected')).toBe('true')
    expect(simTab.getAttribute('aria-selected')).toBe('false')

    fireEvent.keyDown(builderTab, { key: 'ArrowRight' })
    expect(simTab.getAttribute('aria-selected')).toBe('true')
    expect(screen.getByText('+14.2%')).toBeTruthy()

    fireEvent.keyDown(simTab, { key: 'ArrowLeft' })
    expect(builderTab.getAttribute('aria-selected')).toBe('true')
  })

  it('interactively switches template details inside strategy builder with accessible states', () => {
    renderPage()

    // Builder tab is active by default
    expect(screen.getByText('SMA 200 + 5%–15% discount zone')).toBeTruthy()
    const discountPill = screen.getByRole('button', { name: 'Winners at a discount' })
    expect(discountPill.getAttribute('aria-pressed')).toBe('true')

    // Click "The comeback" template pill
    const comebackPill = screen.getByRole('button', { name: 'The comeback' })
    expect(comebackPill.getAttribute('aria-pressed')).toBe('false')
    fireEvent.click(comebackPill)
    expect(comebackPill.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByText('Breakout above 50-day moving average')).toBeTruthy()
    expect(screen.getByText('€150 / step · max 2 steps')).toBeTruthy()

    // Click "Bear markets" template pill
    const bearPill = screen.getByRole('button', { name: 'Bear markets' })
    fireEvent.click(bearPill)
    expect(bearPill.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByText(/S&P 500 drop/)).toBeTruthy()
    expect(screen.getByText('€200 / step · max 5 steps')).toBeTruthy()
  })

  it('expands simulation feature and toggles sub-tabs with accessible role="tab"', () => {
    renderPage()

    // Switch to simulation tab
    fireEvent.click(screen.getByRole('tab', { name: /Real-time simulation/ }))

    expect(screen.getByText('+14.2%')).toBeTruthy()
    expect(screen.getByText('+8.1%')).toBeTruthy()
    expect(screen.getByText('-12.4%')).toBeTruthy()

    const perfTab = screen.getByRole('tab', { name: 'Performance comparison' })
    const tradesTab = screen.getByRole('tab', { name: 'Paper trade log' })
    expect(perfTab.getAttribute('aria-selected')).toBe('true')
    expect(tradesTab.getAttribute('aria-selected')).toBe('false')

    // Toggle sub-tab to paper trades log
    fireEvent.click(tradesTab)
    expect(tradesTab.getAttribute('aria-selected')).toBe('true')
    expect(perfTab.getAttribute('aria-selected')).toBe('false')
    expect(screen.getByText(/Bought 1 step · AAPL/)).toBeTruthy()
    expect(screen.getByText(/Bought 1 step · MSFT/)).toBeTruthy()
  })

  it('expands trading tracker and provides interactive signal execution preview with aria-pressed', () => {
    renderPage()

    // Switch to trading tracker tab
    fireEvent.click(screen.getByRole('tab', { name: /Trading tracker/ }))

    expect(screen.getByText('Zscaler (ZS)')).toBeTruthy()
    expect(screen.getByText(/Dipped 8.2% below 52-week peak/)).toBeTruthy()

    const didItBtn = screen.getByRole('button', { name: 'I did it' })
    const skippedBtn = screen.getByRole('button', { name: 'I skipped it' })
    const snoozeBtn = screen.getByRole('button', { name: 'Remind me tomorrow' })

    expect(didItBtn.getAttribute('aria-pressed')).toBe('false')

    // Click "I did it"
    fireEvent.click(didItBtn)
    expect(didItBtn.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('status').textContent).toMatch(/Trade recorded/)

    // Click "I skipped it"
    fireEvent.click(skippedBtn)
    expect(didItBtn.getAttribute('aria-pressed')).toBe('false')
    expect(skippedBtn.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('status').textContent).toMatch(/Step skipped/)

    // Click "Remind me tomorrow"
    fireEvent.click(snoozeBtn)
    expect(snoozeBtn.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('status').textContent).toMatch(/Snoozed/)
  })

  it('renders fully in Catalan with localized rules and budgets', () => {
    renderPage('ca')

    expect(screen.getByRole('heading', { level: 1, name: 'Opera segons regles, no per emocions.' })).toBeTruthy()
    expect(screen.getByText(/SMA 200 \+ zona de descompte/)).toBeTruthy()
    expect(screen.getByText(/100 € \/ pas · màx\. 3 passos/)).toBeTruthy()

    // Switch template in Catalan
    fireEvent.click(screen.getByRole('button', { name: 'La remuntada' }))
    expect(screen.getByText('Ruptura per sobre de la mitjana de 50 dies')).toBeTruthy()
    expect(screen.getByText('150 € / pas · màx. 2 passos')).toBeTruthy()
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
