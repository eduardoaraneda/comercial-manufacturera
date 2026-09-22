import { defineConfig } from 'vite';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  plugins: [tailwindcss()],
  build: {
    outDir: 'wwwroot/dist',
    emptyOutDir: true,
    rollupOptions: {
      input: {
        login: fileURLToPath(new URL('./src/login/main.ts', import.meta.url)),
        site: fileURLToPath(new URL('./src/site/main.ts', import.meta.url)),
        styles: fileURLToPath(new URL('./src/styles/app.css', import.meta.url))
      },
      output: {
        entryFileNames: '[name].js',
        chunkFileNames: 'chunks/[name]-[hash].js',
        assetFileNames: '[name][extname]'
      }
    }
  }
});
