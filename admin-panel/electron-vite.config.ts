import { defineConfig } from 'electron-vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  main: {
    build: {
      outDir: 'dist-electron',
      rollupOptions: {
        external: [
          'mongodb',
          'protobufjs',
          'chokidar',
          'node-cron',
          'pidusage',
          'electron-store',
        ],
      },
    },
  },
  preload: {
    build: {
      outDir: 'dist-electron',
    },
  },
  renderer: {
    plugins: [react()],
    resolve: {
      alias: {
        '@': path.resolve(__dirname, 'src'),
      },
    },
    server: {
      port: 5173,
    },
  },
});