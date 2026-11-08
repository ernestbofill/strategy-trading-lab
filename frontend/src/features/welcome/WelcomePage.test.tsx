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

  it('interactively switches template rules and watched assets (stocks vs S&P 500 fund)', () => {
    renderPage()

    // Open the builder: "Winners at a discount" is picked first
    fireEvent.click(screen.getByRole('tab', { name: /Strategy builder/ }))
    expect(screen.getByText('SMA 50 > 150 > 200 · 5%–15% discount zone')).toBeTruthy()
    expect(screen.getByText('Watched stocks:')).toBeTruthy()
    expect(screen.getByText('ZS ✓')).toBeTruthy()
    expect(screen.getByText('AAPL ✓')).toBeTruthy()
    const discountPill = screen.getByRole('button', { name: 'Winners at a discount' })
    expect(discountPill.getAttribute('aria-pressed')).toBe('true')

    // Click "The comeback" template pill (stock template)
    const comebackPill = screen.getByRole('button', { name: 'The comeback' })
    expect(comebackPill.getAttribute('aria-pressed')).toBe('false')
    fireEvent.click(comebackPill)
    expect(comebackPill.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByText('≥ 30% below 52w high · +15% above SMA 50 + volume')).toBeTruthy()
    expect(screen.getByText('€150 / step · max 2 steps')).toBeTruthy()
    expect(screen.getByText('Watched stocks:')).toBeTruthy()

    // Click "Bear markets" template pill (fund template: watches SPY, not individual stocks)
    const bearPill = screen.getByRole('button', { name: 'Bear markets' })
    fireEvent.click(bearPill)
    expect(bearPill.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByText('S&P 500 drop ≥ 20% · +5% bounce off low')).toBeTruthy()
    expect(screen.getByText('€200 / step · max 5 steps')).toBeTruthy()
    expect(screen.getByText('The fund it watches:')).toBeTruthy()
    expect(screen.getByText('SPY · S&P 500 ETF ✓')).toBeTruthy()
    expect(screen.queryByText('AAPL ✓')).toBeNull()
  })

  it('expands simulation feature with true minus sign, S&P 500 fund InfoTip, and sub-tab keyboard navigation', () => {
    renderPage()

    // Switch to simulation tab
    fireEvent.click(screen.getByRole('tab', { name: /Real-time simulation/ }))

    expect(screen.getByText('+14.2%')).toBeTruthy()
    expect(screen.getByText('+8.1%')).toBeTruthy()
    // True Unicode minus sign (\u2212), not ASCII hyphen
    expect(screen.getByText('−12.4%')).toBeTruthy()

    // InfoTip on S&P 500 fund
    const infoTipBtn = screen.getByRole('button', { name: 'What is the S&P 500 fund?' })
    fireEvent.click(infoTipBtn)
    expect(
      screen.getByText('Buy and hold: the same money put into an S&P 500 fund (SPY) on the same days, never sold.')
    ).toBeTruthy()

    const perfTab = screen.getByRole('tab', { name: 'Performance comparison' })
    const tradesTab = screen.getByRole('tab', { name: 'Trade log' })
    expect(perfTab.getAttribute('aria-selected')).toBe('true')
    expect(perfTab.getAttribute('tabIndex')).toBe('0')
    expect(tradesTab.getAttribute('aria-selected')).toBe('false')
    expect(tradesTab.getAttribute('tabIndex')).toBe('-1')

    // Arrow key navigation on simulation sub-tabs
    fireEvent.keyDown(perfTab, { key: 'ArrowRight' })
    expect(tradesTab.getAttribute('aria-selected')).toBe('true')
    expect(document.activeElement).toBe(tradesTab)
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

  it('renders fully in Catalan with glossary-compliant rules, funds, and budgets', () => {
    renderPage('ca')
    fireEvent.click(screen.getByRole('tab', { name: /Constructor|Creador|estrat/i }))

    expect(screen.getByRole('heading', { level: 1, name: 'Opera segons regles, no per emocions.' })).toBeTruthy()
    expect(screen.getByText('SMA 50 > 150 > 200 · zona de descompte 5%–15%')).toBeTruthy()
    expect(screen.getByText('100 € / fase · màx. 3 fases')).toBeTruthy()
    expect(screen.getByText('Accions a vigilar:')).toBeTruthy()

    // Switch template in Catalan to "Empreses en remuntada"
    fireEvent.click(screen.getByRole('button', { name: 'Empreses en remuntada' }))
    expect(screen.getByText('≥ 30% sota el màxim anual · +15% sobre SMA 50 + volum')).toBeTruthy()
    expect(screen.getByText('150 € / fase · màx. 2 fases')).toBeTruthy()

    // Switch template in Catalan to "Mercats baixistes"
    fireEvent.click(screen.getByRole('button', { name: 'Mercats baixistes' }))
    expect(screen.getByText('El fons que vigila:')).toBeTruthy()
    expect(screen.getByText('SPY · Fons S&P 500 ✓')).toBeTruthy()
  })

  it('renders trust pillars, bottom call to action, and site footer disclaimer', () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 2, name: 'Built for transparency and control' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 3, name: 'Zero broker access' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 3, name: 'Uncompromising benchmark honesty' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 3, name: 'Disciplined systematic rules' })).toBeTruthy()

    expect(screen.getByRole('heading', { level: 2, name: 'Ready to test your first strategy?' })).toBeTruthy()
    expect(screen.getByText('Not financial advice. Signals follow your rules.')).toBeTruthy()
  })

  it('replaces DASHBOARD with BACK on top bar when navigated with ?from=new for authenticated users', () => {
    mockRole = 'user'
    renderPage('en', ['/welcome?from=new'])

    const backLink = screen.getByRole('link', { name: 'BACK' })
    expect(backLink.getAttribute('href')).toBe('/new')
    expect(screen.queryByRole('link', { name: 'DASHBOARD' })).toBeNull()
    expect(screen.getByRole('link', { name: 'ACCOUNT' })).toBeTruthy()
    // Authenticated users never see the guest CTA
    expect(screen.queryByRole('heading', { level: 2, name: 'Ready to test your first strategy?' })).toBeNull()
  })

  it('hides bottom CTA banner for authenticated users and shows DASHBOARD when not ?from=new', () => {
    mockRole = 'user'
    renderPage('en', ['/welcome'])

    expect(screen.getByRole('link', { name: 'DASHBOARD' })).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'BACK' })).toBeNull()
    expect(screen.queryByRole('heading', { level: 2, name: 'Ready to test your first strategy?' })).toBeNull()
  })

  it('shows BACK on top bar and keeps bottom CTA for guests when navigated with ?from=new', () => {
    mockRole = null
    renderPage('en', ['/welcome?from=new'])

    const backLink = screen.getByRole('link', { name: 'BACK' })
    expect(backLink.getAttribute('href')).toBe('/new')
    expect(screen.getByRole('link', { name: 'LOG IN' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 2, name: 'Ready to test your first strategy?' })).toBeTruthy()
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
