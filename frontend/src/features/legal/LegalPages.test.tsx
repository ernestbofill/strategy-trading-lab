// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { LegalPage } from './LegalPage'
import { LegalNav } from './LegalNav'
import { termsPath } from './paths'

afterEach(cleanup)

function wrap(ui: React.ReactElement, initialPath = '/') {
  return render(<MemoryRouter initialEntries={[initialPath]}>{ui}</MemoryRouter>)
}

describe('LegalPage', () => {
  it('renders legal sections', () => {
    wrap(<LegalPage section="notice" />)
    expect(screen.getByRole('heading', { level: 1, name: /Legal notice/i })).toBeTruthy()

    cleanup()
    wrap(<LegalPage section="terms" />)
    expect(screen.getByRole('heading', { level: 1, name: /Terms of use/i })).toBeTruthy()
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
})
