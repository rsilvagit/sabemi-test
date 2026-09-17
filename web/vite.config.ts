import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      // Mirrors what nginx does in Docker (web/nginx.conf.template): injects the dashboard's
      // ApiKey server-side so `npm run dev` outside Docker still hits the authenticated
      // /api/payments. Dev-only default key — same one used in
      // appsettings.Development.json's Dashboard:ApiKey.
      '/api': {
        target: 'http://localhost:8080',
        headers: { 'X-Api-Key': 'dev-local-dashboard-key' },
      },
      '/webhooks': 'http://localhost:8080',
    },
  },
})
