// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, ExpiredLinkError } from '../../api'
import { Unsubscribe } from './Unsubscribe'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

function open(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/unsubscribe/:token/:kind" element={<Unsubscribe />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Unsubscribe', () => {
  it('changes nothing until the button is pressed, then says what was turned off', async () => {
    const unsubscribe = vi.spyOn(api, 'unsubscribe').mockResolvedValue()
    open('/unsubscribe/tok3n/alerts')

    expect(unsubscribe).not.toHaveBeenCalled()
    expect(screen.getByText(/stop getting signal alerts and reminders/i)).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Turn off' }))

    expect(await screen.findByText(/No more signal alerts or reminders/i)).toBeTruthy()
    expect(unsubscribe).toHaveBeenCalledWith('tok3n', 'alerts')
    expect(screen.getByRole('link', { name: 'Go to your account' })).toBeTruthy()
  })

  it('says so when the link no longer works, and for a kind it does not know', async () => {
    vi.spyOn(api, 'unsubscribe').mockRejectedValue(new ExpiredLinkError())
    open('/unsubscribe/old/overview')
    fireEvent.click(screen.getByRole('button', { name: 'Turn off' }))
    expect(await screen.findByText(/no longer works/i)).toBeTruthy()
    cleanup()

    open('/unsubscribe/tok3n/everything')
    expect(screen.getByText(/no longer works/i)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Turn off' })).toBeNull()
  })

  it('lets the user try again after a failure', async () => {
    vi.spyOn(api, 'unsubscribe').mockRejectedValueOnce(new Error('down')).mockResolvedValueOnce()
    open('/unsubscribe/tok3n/overview')

    fireEvent.click(screen.getByRole('button', { name: 'Turn off' }))
    expect(await screen.findByText(/Couldn't turn them off/i)).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Turn off' }))
    expect(await screen.findByText(/No more daily overviews/i)).toBeTruthy()
  })
})
