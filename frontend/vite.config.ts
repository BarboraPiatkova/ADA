import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In dev, the SPA calls the API through this proxy — same origin as in production
// (where the API serves the built SPA), so no CORS setup is needed in either case.
const apiTarget = process.env.API_URL ?? 'http://localhost:8080'

// The API sends these headers in production (src/AdaPlatform.Api/Security/SecurityHeaders.cs —
// keep the two in step). `npm run preview` serves the built app under the same policy, so a
// CSP violation shows up before deployment. Not on the dev server: its hot reload injects
// inline scripts.
const productionHeaders = {
  'Content-Security-Policy': [
    "default-src 'self'",
    "script-src 'self'",
    "style-src 'self'",
    "img-src 'self' data: https://tile.openstreetmap.org https://api.mapy.com",
    "font-src 'self'",
    "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join('; '),
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
}

export default defineConfig({
  plugins: [tailwindcss(), react()],
  server: {
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
  preview: {
    headers: productionHeaders,
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
})
