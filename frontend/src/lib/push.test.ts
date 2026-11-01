// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api'
import { forgetPushKey, pushSupport, subscribeDevice } from './push'

const key = 'BAAA' // Any base64url: the bytes only need to match.

function browser({ permission = 'default', existing = null as null | { key: Uint8Array } } = {}) {
  const unsubscribe = vi.fn().mockResolvedValue(true)
  const made = { endpoint: 'https://fcm.googleapis.com/fcm/send/x', toJSON: () => ({ keys: { p256dh: 'p', auth: 'a' } }), unsubscribe }
  const pushManager = {
    getSubscription: vi.fn().mockResolvedValue(existing && { ...made, options: { applicationServerKey: existing.key.buffer }, unsubscribe }),
    subscribe: vi.fn().mockResolvedValue({ ...made, options: {} }),
  }
  vi.stubGlobal('navigator', { ...navigator, serviceWorker: { ready: Promise.resolve({ pushManager }) } })
  vi.stubGlobal('PushManager', function PushManager() {})
  const requestPermission = vi.fn().mockResolvedValue('granted')
  vi.stubGlobal('Notification', { permission, requestPermission })
  return { pushManager, unsubscribe, requestPermission }
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  forgetPushKey()
})

describe('push', () => {
  it('is unsupported without a service worker, and blocked once the user refused', () => {
    expect(pushSupport()).toBe('unsupported')
    browser({ permission: 'denied' })
    expect(pushSupport()).toBe('blocked')
  })

  it('asks for permission, subscribes with the server key and saves the device', async () => {
    const { pushManager, requestPermission } = browser()
    vi.spyOn(api, 'getPushKey').mockResolvedValue(key)
    const save = vi.spyOn(api, 'savePushSubscription').mockResolvedValue()

    expect(await subscribeDevice()).toBe(true)
    expect(requestPermission).toHaveBeenCalled()
    expect(pushManager.subscribe).toHaveBeenCalledWith(expect.objectContaining({ userVisibleOnly: true }))
    expect(save).toHaveBeenCalledWith({ endpoint: 'https://fcm.googleapis.com/fcm/send/x', keys: { p256dh: 'p', auth: 'a' } })
  })

  it('replaces a subscription made with another key', async () => {
    const { pushManager, unsubscribe } = browser({ permission: 'granted', existing: { key: new Uint8Array([9, 9, 9]) } })
    vi.spyOn(api, 'getPushKey').mockResolvedValue(key)
    vi.spyOn(api, 'savePushSubscription').mockResolvedValue()

    expect(await subscribeDevice()).toBe(true)
    expect(unsubscribe).toHaveBeenCalled()
    expect(pushManager.subscribe).toHaveBeenCalled()
  })

  it('subscribes nothing when the user refuses or the server sends no push', async () => {
    const refused = browser()
    refused.requestPermission.mockResolvedValue('denied')
    expect(await subscribeDevice()).toBe(false)

    const noKey = browser({ permission: 'granted' })
    vi.spyOn(api, 'getPushKey').mockResolvedValue(null)
    expect(await subscribeDevice()).toBe(false)
    expect(noKey.pushManager.subscribe).not.toHaveBeenCalled()
  })
})
