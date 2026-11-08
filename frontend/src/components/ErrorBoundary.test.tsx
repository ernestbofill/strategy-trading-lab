// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErrorBoundary } from './ErrorBoundary'

const Fine = () => <p>Fine page</p>
const Broken = (): never => {
  throw new Error('broken')
}

afterEach(cleanup)

describe('ErrorBoundary', () => {
  it('catches a page that crashes as it opens, and clears the crash on the next path', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    const { rerender } = render(<ErrorBoundary resetKey="/a"><Fine /></ErrorBoundary>)

    rerender(<ErrorBoundary resetKey="/b"><Broken /></ErrorBoundary>)
    expect(screen.getByRole('alert')).toBeTruthy()

    rerender(<ErrorBoundary resetKey="/c"><Fine /></ErrorBoundary>)
    expect(screen.queryByRole('alert')).toBeNull()
    expect(screen.getByText('Fine page')).toBeTruthy()
  })
})
