import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const apiTarget = process.env.API_URL ?? 'http://localhost:5080';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // The SPA only ever calls /api. In dev this proxy strips the prefix; in a container nginx
      // does the same. Keeping both rewrites identical is what stops "works in dev" surprises.
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
        headers: {
          // The API builds absolute review links from the request. Dev sends the same forwarded
          // headers the reverse proxy sends in production, so the links match in both.
          'X-Forwarded-Proto': 'http',
          'X-Forwarded-Host': 'localhost:5173',
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
});
