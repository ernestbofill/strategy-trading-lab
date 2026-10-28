// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { lazyPage } from '../lib/lazyPage'
import { LazyPage } from './LazyPage'

const reload = vi.fn()

beforeEach(() => {
  sessionStorage.clear()
  vi.stubGlobal('location', { ...window.location, reload })
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  reload.mockReset()
})

function show(load: () => Promise<() => React.ReactNode>) {
  const Page = lazyPage(load)
  render(
    <MemoryRouter>
      <LazyPage back="/admin">
        <Page />
      </LazyPage>
    </MemoryRouter>,
  )
}

const gone = () => Promise.reject(new TypeError('Failed to fetch dynamically imported module'))

describe('LazyPage', () => {
  it('shows the page once its chunk loads, and forgets an earlier reload', async () => {
    sessionStorage.setItem('stl.chunkReload', '1')
    show(() => Promise.resolve(() => <h1>Assets</h1>))
    expect(screen.getByText('Loading…')).toBeTruthy()
    expect(await screen.findByRole('heading', { name: 'Assets' })).toBeTruthy()
    expect(sessionStorage.getItem('stl.chunkReload')).toBeNull()
  })

  it('reloads once onto the new build when the chunk is gone after a deploy', async () => {
    show(gone)
    await vi.waitFor(() => expect(reload).toHaveBeenCalledOnce())
    expect(screen.queryByText(/Couldn't load/)).toBeNull()
  })

  it('shows a message instead of a blank app when it fails again after the reload', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    sessionStorage.setItem('stl.chunkReload', '1')
    show(gone)
    expect(await screen.findByText("Couldn't load this page. Reload to try again.")).toBeTruthy()
    expect(reload).not.toHaveBeenCalled()
  })
})
