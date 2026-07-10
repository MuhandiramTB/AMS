import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// Vitest config kept separate from vite.config.ts so the dev-server (proxy)
// settings and the test settings don't entangle.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    include: ['src/**/*.test.{ts,tsx}'],
  },
});
