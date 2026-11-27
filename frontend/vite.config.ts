import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// In development the API runs on :5030 (dotnet run) and is proxied under /api.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': { target: process.env.AMMA_API ?? 'http://localhost:5030', changeOrigin: true } },
  },
  build: {
    // `npm run build:api` places the app in the API's wwwroot so one server hosts both.
    outDir: process.env.AMMA_OUT ?? 'dist',
    emptyOutDir: true,
  },
})
