import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './styles/global.css'
import App from './App.tsx'
import { registerSW } from 'virtual:pwa-register'
import { initializePwaInstall } from './lib/pwaInstall'
import { refreshAll } from './lib/useLoadable'

// With autoUpdate, a new service worker takes over by itself and this reloads the page onto it,
// so a deploy shows up without a hard reload. The hourly check covers tabs left open.
initializePwaInstall()
registerSW({
  immediate: true,
  onRegisteredSW(_url, registration) {
    if (registration) setInterval(() => void registration.update(), 60 * 60 * 1000)
  },
})

// The server changes data on its own (a new signal, an AI round due), so open screens reload it when the app comes back
// to the front (unless loaded in the last 30 s), every few minutes while it is in front, and when a push arrives (push-sw.js).
document.addEventListener('visibilitychange', () => document.visibilityState === 'visible' && refreshAll(30 * 1000))
setInterval(() => document.visibilityState === 'visible' && refreshAll(), 5 * 60 * 1000)
navigator.serviceWorker?.addEventListener('message', (event) => event.data?.type === 'stl:refresh' && refreshAll())

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
