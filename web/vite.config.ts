import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      // Mirrors what nginx does in Docker (web/nginx.conf.template): injects the ApiKey
      // server-side so `npm run dev` outside Docker still hits the authenticated
      // /api/payments. Dev-only default key — same one used in appsettings.json/.env.example.
      '/api': {
        target: 'http://localhost:8080',
        headers: { 'X-Api-Key': 'dev-local-key' },
      },
      '/webhooks': 'http://localhost:8080',
    },
  },
})
