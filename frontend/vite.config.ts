/// <reference types="vitest/config" />
import { readFileSync } from 'node:fs'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

// The deploy workflow sets APP_VERSION from main's commit count; local builds keep package.json's 0.0.0-dev.
const { version } = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8')) as { version: string }
const appVersion = process.env.APP_VERSION || version

// As nginx does in production: /api/templates reaches the API's /templates (`dotnet run` in backend/src/StrategyTradingLab.Api).
// STL_API_URL points it at an API on another port, e.g. a second worktree's while the first one holds 5184.
const proxy = { '/api': { target: process.env.STL_API_URL ?? 'http://localhost:5184', rewrite: (path: string) => path.replace(/^\/api/, '') } }

// https://vite.dev/config/
export default defineConfig({
  define: { __APP_VERSION__: JSON.stringify(appVersion) },
  server: { proxy },
  preview: { proxy },
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      // Opening the API in a tab must reach the server, not the app shell; admin chunks load only on /admin.
      // push-sw.js shows the web push notifications and opens the app on a tap.
      workbox: { navigateFallbackDenylist: [/^\/api\//], globIgnores: ['**/Admin*'], importScripts: ['push-sw.js'] },
      manifest: {
        name: 'Strategy Trading Lab',
        short_name: 'STL',
        description: 'Pick, test and follow simple trading strategies.',
        theme_color: '#100d23',
        background_color: '#261d45',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: 'icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
          { src: 'icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          { src: 'icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
        ],
      },
    }),
  ],
  test: { setupFiles: ['src/testSetup.ts'] },
})
