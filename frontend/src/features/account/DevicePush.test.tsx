// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { pushAsks } from '../../lib/push'
import { PushPrompt } from './DevicePush'

vi.mock('../../lib/push', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../lib/push')>()),
  pushSupport: () => 'ready',
  subscribeDevice: vi.fn().mockResolvedValue(true),
}))

beforeEach(() => {
  localStorage.clear()
  vi.stubGlobal('Notification', { permission: 'default' })
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('PushPrompt asks at most twice per device', () => {
  it('asks on the dashboard the first time, with Not now', () => {
    const done = vi.fn()
    render(<PushPrompt onDone={done} />)

    expect(screen.getByRole('button', { name: 'Allow notifications' })).toBeTruthy()
    expect(pushAsks()).toBe(1)
    fireEvent.click(screen.getByRole('button', { name: 'Not now' }))
    expect(done).toHaveBeenCalled()
  })

  it("doesn't ask on the dashboard again once asked", () => {
    localStorage.setItem('stl.pushAsks', '1')
    const done = vi.fn()
    render(<PushPrompt onDone={done} />)

    expect(screen.queryByRole('region')).toBeNull()
    expect(done).toHaveBeenCalled()
  })

  it('asks a second time on Confirm, without Not now, and then never again', () => {
    localStorage.setItem('stl.pushAsks', '1')
    render(<PushPrompt place="confirm" onDone={() => {}} />)

    expect(screen.getByRole('button', { name: 'Allow notifications' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Not now' })).toBeNull()
    expect(pushAsks()).toBe(2)
    cleanup()

    render(<PushPrompt place="confirm" onDone={() => {}} />)
    expect(screen.queryByRole('region')).toBeNull()
  })

  it("doesn't ask on Confirm before the dashboard has, nor once blocked", () => {
    render(<PushPrompt place="confirm" onDone={() => {}} />)
    expect(screen.queryByRole('region')).toBeNull()

    localStorage.setItem('stl.pushAsks', '1')
    vi.stubGlobal('Notification', { permission: 'denied' })
    render(<PushPrompt place="confirm" onDone={() => {}} />)
    expect(screen.queryByRole('region')).toBeNull()
  })
})
