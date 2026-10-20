import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: './e2e', fullyParallel: false, workers: 1,
    use: { baseURL: 'http://localhost:64423', viewport: { width: 1440, height: 1000 }, trace: 'retain-on-failure' },
    webServer: { command: 'npm run dev', url: 'http://localhost:64423', reuseExistingServer: false, timeout: 30000 },
});
