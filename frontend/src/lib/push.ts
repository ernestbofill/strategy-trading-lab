import { api } from '../api'
import { isStandalone } from './pwaInstall'

/**
 * What this device can do with web push (spec: Notifications):
 * - `ready`: it can ask for permission now;
 * - `homeScreen`: an iPhone or iPad in Safari, which only gets notifications once the app is on the home screen;
 * - `blocked`: the user refused notifications for the app in the browser, so only the browser's settings can undo it;
 * - `unsupported`: no web push in this browser.
 */
export type PushSupport = 'ready' | 'homeScreen' | 'blocked' | 'unsupported'

const isAppleMobile = () =>
  /iPhone|iPad|iPod/.test(navigator.userAgent) || (navigator.userAgent.includes('Macintosh') && navigator.maxTouchPoints > 1)

export function pushSupport(): PushSupport {
  if (typeof window === 'undefined' || !('serviceWorker' in navigator)) return 'unsupported'
  if (!('PushManager' in window) || !('Notification' in window)) return isAppleMobile() && !isStandalone() ? 'homeScreen' : 'unsupported'
  return Notification.permission === 'denied' ? 'blocked' : 'ready'
}

/** The key the server's push messages are signed with, asked for once per page load. */
let keyRequest: Promise<string | null> | null = null
export function pushKey(): Promise<string | null> {
  keyRequest ??= api.getPushKey().catch((error: unknown) => {
    keyRequest = null
    throw error
  })
  return keyRequest
}

/** For tests: forget the key asked for. */
export function forgetPushKey() {
  keyRequest = null
}

function keyBytes(base64Url: string): Uint8Array<ArrayBuffer> {
  const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(base64Url.length / 4) * 4, '=')
  return Uint8Array.from(atob(base64), (c) => c.charCodeAt(0))
}

const sameKey = (a: ArrayBuffer | null, b: Uint8Array) => a !== null && a.byteLength === b.byteLength && new Uint8Array(a).every((x, i) => x === b[i])

/** This device's subscription, if it has one the server can send to. */
export async function deviceSubscription(): Promise<PushSubscription | null> {
  if (pushSupport() !== 'ready' || Notification.permission !== 'granted') return null
  return (await navigator.serviceWorker.ready).pushManager.getSubscription()
}

/**
 * Asks for permission (call it from a tap: Safari only asks then), subscribes this device and tells the server. Returns
 * false when the user refused or the server doesn't send push. A subscription made with an older key is replaced.
 */
export async function subscribeDevice(): Promise<boolean> {
  if (pushSupport() !== 'ready') return false
  // Asked before anything else is awaited, while the tap still counts as the user's.
  const permission = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission()
  if (permission !== 'granted') return false
  const key = await pushKey()
  if (!key) return false
  const applicationServerKey = keyBytes(key)
  const { pushManager } = await navigator.serviceWorker.ready
  let subscription = await pushManager.getSubscription()
  if (subscription && !sameKey(subscription.options.applicationServerKey, applicationServerKey)) {
    await subscription.unsubscribe()
    subscription = null
  }
  subscription ??= await pushManager.subscribe({ userVisibleOnly: true, applicationServerKey })
  const json = subscription.toJSON()
  await api.savePushSubscription({ endpoint: subscription.endpoint, keys: { p256dh: json.keys?.p256dh ?? '', auth: json.keys?.auth ?? '' } })
  return true
}

/** Saves this device's subscription again, for the account now signed in. Never asks: only where notifications are allowed. */
export async function claimDevice(): Promise<void> {
  try {
    if (await deviceSubscription()) await subscribeDevice()
  } catch {
    // Best effort: the account page shows the device's state.
  }
}

/** Stops notifications on this device: the server forgets it, then the browser does. */
export async function unsubscribeDevice(): Promise<void> {
  const subscription = await deviceSubscription()
  if (!subscription) return
  await api.removePushSubscription(subscription.endpoint)
  await subscription.unsubscribe()
}

const ASKS_KEY = 'stl.pushAsks'

/**
 * How many times this device was asked to allow notifications (spec: Notifications): at most twice, on the dashboard after
 * the first live or AI strategy starts, then on Confirm of the next one. Per device, since the permission is; 0 without storage.
 */
export function pushAsks(): number {
  try {
    return Number(localStorage.getItem(ASKS_KEY)) || 0
  } catch {
    return 0
  }
}

export function notePushAsk(): void {
  try {
    localStorage.setItem(ASKS_KEY, String(pushAsks() + 1))
  } catch {
    // No storage: the device may be asked again, which is harmless.
  }
}
