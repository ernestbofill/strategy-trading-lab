// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { LegalPage } from './LegalPage'
import { LegalNav } from './LegalNav'
import { legalNoticePath, termsPath } from './paths'

afterEach(cleanup)

function wrap(ui: React.ReactElement, initialPath = '/') {
  return render(<MemoryRouter initialEntries={[initialPath]}>{ui}</MemoryRouter>)
}

describe('LegalPage', () => {
  it('renders legal notice with operator and hosting details', () => {
    wrap(<LegalPage section="notice" />)
    expect(screen.getByRole('heading', { level: 1, name: /Legal notice/i })).toBeTruthy()
    expect(screen.getAllByText(/Ernest Bofill/i).length).toBeGreaterThan(0)
    expect(screen.getAllByText(/Barcelona, Spain/i).length).toBeGreaterThan(0)
    expect(screen.getByText(/Oracle Cloud Infrastructure/i)).toBeTruthy()
    expect(screen.getByText(/Neon, Inc\./i)).toBeTruthy()
  })

  it('renders terms of use with platform scope and governing law', () => {
    wrap(<LegalPage section="terms" />)
    expect(screen.getByRole('heading', { level: 1, name: /Terms of use/i })).toBeTruthy()
    expect(screen.getByText(/Simulation mode/i)).toBeTruthy()
    expect(screen.getByText(/Live mode/i)).toBeTruthy()
    expect(screen.getAllByText(/Barcelona, Spain/i).length).toBeGreaterThan(0)
  })

  it('renders privacy policy with GDPR controller and cookie disclosure', () => {
    wrap(<LegalPage section="privacy" />)
    expect(screen.getByRole('heading', { level: 1, name: /Privacy policy/i })).toBeTruthy()
    expect(screen.getByText(/stl_session/i)).toBeTruthy()
    expect(screen.getByText(/Performance of a contract/i)).toBeTruthy()
  })

  it('renders disclaimer with full disclaimers and risks', () => {
    wrap(<LegalPage section="disclaimer" />)
    expect(screen.getByRole('heading', { level: 1, name: /Disclaimer/i })).toBeTruthy()
    expect(screen.getAllByText(/Not financial advice/i).length).toBeGreaterThan(0)
    expect(screen.getByText(/Risk of capital loss/i)).toBeTruthy()
  })

  it('renders about page with conflicts of interest disclosure', () => {
    wrap(<LegalPage section="about" />)
    expect(screen.getByRole('heading', { level: 1, name: /^About$/i })).toBeTruthy()
    expect(screen.getByText(/Conflicts of interest disclosure/i)).toBeTruthy()
    expect(screen.getByText(/Zero broker kickbacks/i)).toBeTruthy()
  })
})

describe('LegalNav', () => {
  it('highlights the current legal page with aria-current="page"', () => {
    wrap(<LegalNav />, termsPath)
    const termsLink = screen.getByRole('link', { name: /Terms of use/i })
    expect(termsLink.getAttribute('aria-current')).toBe('page')

    const noticeLink = screen.getByRole('link', { name: /Legal notice/i })
    expect(noticeLink.getAttribute('aria-current')).toBeNull()
  })

  it('navigates to legal notice', () => {
    wrap(<LegalNav />, legalNoticePath)
    const noticeLink = screen.getByRole('link', { name: /Legal notice/i })
    expect(noticeLink.getAttribute('aria-current')).toBe('page')
  })
})
