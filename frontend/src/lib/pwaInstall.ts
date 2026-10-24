export interface PwaInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>
}

interface PwaInstallState {
  prompt: PwaInstallPromptEvent | null
  installed: boolean
}

declare global {
  interface WindowEventMap {
    beforeinstallprompt: PwaInstallPromptEvent
    appinstalled: Event
  }
}

const listeners = new Set<() => void>()
let state: PwaInstallState = { prompt: null, installed: false }
let initialized = false

function notify() {
  listeners.forEach((listener) => listener())
}

function isStandalone() {
  const displayMode = typeof window.matchMedia === 'function' && window.matchMedia('(display-mode: standalone)').matches
  return displayMode || Reflect.get(navigator, 'standalone') === true
}

export function initializePwaInstall() {
  if (initialized || typeof window === 'undefined') return
  initialized = true
  state = { prompt: null, installed: isStandalone() }

  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault()
    state = { ...state, prompt: event }
    notify()
  })

  window.addEventListener('appinstalled', () => {
    state = { prompt: null, installed: true }
    notify()
  })
}

export function subscribePwaInstall(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function getPwaInstallState() {
  return state
}

export function consumePwaInstallPrompt(prompt: PwaInstallPromptEvent) {
  if (state.prompt !== prompt) return
  state = { ...state, prompt: null }
  notify()
}
