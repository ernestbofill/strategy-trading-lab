// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { initializePwaInstall, type PwaInstallPromptEvent } from '../../lib/pwaInstall'
import { InstallApp } from './InstallApp'

afterEach(cleanup)

class MockInstallPromptEvent extends Event implements PwaInstallPromptEvent {
  prompt = vi.fn().mockResolvedValue(undefined)
  userChoice = Promise.resolve({ outcome: 'accepted' as const, platform: 'web' })

  constructor() {
    super('beforeinstallprompt', { cancelable: true })
  }
}

describe('InstallApp', () => {
  it('offers browser instructions when no prompt is available and uses the native prompt when it arrives', async () => {
    initializePwaInstall()
    render(<InstallApp />)

    fireEvent.click(screen.getByRole('button', { name: 'Install the app' }))
    expect((await screen.findByRole('status')).textContent).toContain('On iPhone or iPad')

    const event = new MockInstallPromptEvent()
    act(() => window.dispatchEvent(event))
    expect(event.defaultPrevented).toBe(true)

    fireEvent.click(screen.getByRole('button', { name: 'Install the app' }))
    expect(event.prompt).toHaveBeenCalledOnce()
    expect((await screen.findByRole('status')).textContent).toBe('Installation started.')

    act(() => window.dispatchEvent(new Event('appinstalled')))
    expect(await screen.findByText('The app is installed on this device.')).toBeTruthy()
  })
})
