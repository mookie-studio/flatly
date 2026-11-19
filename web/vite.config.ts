import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  // No public sourcemaps in production.
  build: { sourcemap: false },
  // Local development only: Aspire sets services__api__* to the API address (see api/src/Flatly.AppHost).
  // /api is proxied to it, so the browser makes same-origin calls and the API needs no CORS for local runs.
  server: {
    proxy: process.env.services__api__http__0
      ? { '/api': { target: process.env.services__api__http__0, changeOrigin: true } }
      : undefined,
  },
  test: {
    include: ['src/**/*.test.{ts,tsx}'],
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
