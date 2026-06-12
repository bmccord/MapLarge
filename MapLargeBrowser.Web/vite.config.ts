import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    port: 5081,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5080'
      }
    }
  }
});
