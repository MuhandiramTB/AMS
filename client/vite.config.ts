import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Dev server proxies /api to the ASP.NET Core API so the SPA and API share an
// origin during development (avoids CORS friction locally). Port 5173 is the
// origin allow-listed by the API's CORS policy (06 §13).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
