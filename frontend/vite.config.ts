import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg'],
      manifest: {
        name: 'Sys Pitstops',
        short_name: 'Pitstops',
        description: 'Gestão de oficina mecânica',
        lang: 'pt-BR',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        background_color: '#0b1220',
        theme_color: '#0b1220',
        icons: [
          { src: '/pwa-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/pwa-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/pwa-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        // The shell is precached so the app opens with no network. The API is
        // never part of the precache — only of the runtime read cache below.
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api\//, /^\/swagger/],
        runtimeCaching: [
          {
            // D-16: cache of reads, and only reads. There is no write queue,
            // so a POST offline fails at the screen instead of being replayed
            // later against a service order that moved on.
            urlPattern: ({ url, request }) =>
              request.method === 'GET' &&
              url.pathname.startsWith('/api/') &&
              !url.pathname.startsWith('/api/auth/'),
            handler: 'NetworkFirst',
            options: {
              cacheName: 'api-reads',
              networkTimeoutSeconds: 5,
              expiration: { maxEntries: 200, maxAgeSeconds: 60 * 60 * 24 },
              cacheableResponse: { statuses: [200] },
            },
          },
        ],
      },
      devOptions: { enabled: false },
    }),
  ],
  server: {
    port: 5173,
    proxy: {
      // D-20: the token rides in an httpOnly cookie with SameSite=Lax, which
      // only works if the front and the API are the same origin. The proxy is
      // what makes that true in development; in production D-26 does it by
      // serving this bundle from the ASP.NET service itself.
      '/api': { target: 'http://localhost:5000', changeOrigin: false },
    },
  },
})
