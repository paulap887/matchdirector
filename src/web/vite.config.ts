import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Aspire injects the overlay hub address; proxy its API and SignalR hub so the app is same-origin in dev and prod.
const overlayHub = process.env.services__overlayhub__https__0 ?? process.env.services__overlayhub__http__0 ?? 'http://localhost:5187'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: overlayHub, changeOrigin: true, secure: false },
      '/hubs': { target: overlayHub, changeOrigin: true, secure: false, ws: true },
    },
  },
})
