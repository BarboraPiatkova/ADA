import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In dev, the SPA calls the API through this proxy — same origin as in production
// (where the API serves the built SPA), so no CORS setup is needed in either case.
const apiTarget = process.env.API_URL ?? 'http://localhost:8080'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
})
