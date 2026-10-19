import { readFileSync } from 'node:fs'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

const { version } = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8')) as { version: string }

// https://vite.dev/config/
export default defineConfig({
  define: { __APP_VERSION__: JSON.stringify(version) },
  // As nginx does in production: /api/templates reaches the API's /templates (`dotnet run` in backend/src/StrategyTradingLab.Api).
  server: {
    proxy: { '/api': { target: 'http://localhost:5184', rewrite: (path) => path.replace(/^\/api/, '') } },
  },
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      // Opening the API in a tab must reach the server, not the app shell.
      workbox: { navigateFallbackDenylist: [/^\/api\//] },
      manifest: {
        name: 'Strategy Trading Lab',
        short_name: 'STL',
        description: 'Pick, test and follow simple trading strategies.',
        theme_color: '#100d23',
        background_color: '#261d45',
        display: 'standalone',
        start_url: '/',
        icons: [{ src: 'icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' }],
      },
    }),
  ],
})
