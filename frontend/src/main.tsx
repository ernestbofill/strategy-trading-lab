import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './styles/global.css'
import App from './App.tsx'
import { registerSW } from 'virtual:pwa-register'

// With autoUpdate, a new service worker takes over by itself and this reloads the page onto it,
// so a deploy shows up without a hard reload. The hourly check covers tabs left open.
registerSW({
  immediate: true,
  onRegisteredSW(_url, registration) {
    if (registration) setInterval(() => void registration.update(), 60 * 60 * 1000)
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
