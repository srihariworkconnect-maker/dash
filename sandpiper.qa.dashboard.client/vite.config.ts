import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { env } from 'node:process';

export default defineConfig({
    base: './',
    plugins: [react()],
    server: {
        host: 'localhost',
        port: 64423,
        strictPort: true,
        proxy: {
            '/api': {
                target: env.SANDPIPER_API_PROXY_TARGET || 'http://localhost:5243',
                changeOrigin: false,
            },
        },
    },
});
