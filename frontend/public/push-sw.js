// Web push, imported into the generated service worker (vite.config.ts, workbox.importScripts).
// The server sends { title, body, url, tag } (backend Notifications/PushTexts.cs).

self.addEventListener('push', (event) => {
  let message = {}
  try {
    message = event.data ? event.data.json() : {}
  } catch {
    // Not ours to read: show nothing special.
  }
  const title = message.title || 'Strategy Trading Lab'
  event.waitUntil(
    self.registration.showNotification(title, {
      body: message.body || '',
      tag: message.tag,
      icon: '/icon-192.png',
      badge: '/icon-192.png',
      // Only app paths are opened.
      data: { url: typeof message.url === 'string' && message.url.startsWith('/') && !message.url.startsWith('//') ? message.url : '/' },
    }),
  )
})

// A tap opens the signal (or the round) in the app's window if one is open, else in a new one.
self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const url = new URL(event.notification.data?.url || '/', self.location.origin).href
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      const open = windows.find((client) => new URL(client.url).origin === self.location.origin)
      if (open) return open.focus().then((client) => client.navigate(url))
      return self.clients.openWindow(url)
    }),
  )
})
