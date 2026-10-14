import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';

// Q-231: the SPA is built into the API's wwwroot and served from the same origin, so every API
// call is a relative /api/v1 path and the refresh cookie (Q-232, SameSite=Strict) just works.
// In `vite dev` the API is proxied so the same relative paths resolve.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  build: { outDir: '../SchoolSync.Api/wwwroot', emptyOutDir: true },
  server: {
    proxy: {
      '/api': { target: 'https://localhost:7001', secure: false },
      '/hubs': { target: 'https://localhost:7001', secure: false, ws: true },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
});
