import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './e2e', timeout: 60000, workers: 1,
  use: { baseURL: process.env.CAREERGAP_BASE_URL ?? 'http://127.0.0.1:5173', headless: true },
  outputDir: process.env.CAREERGAP_E2E_OUTPUT_DIRECTORY ?? 'test-results',
  reporter: process.env.CAREERGAP_E2E_JSON ? [['list'], ['json', { outputFile: process.env.CAREERGAP_E2E_JSON }]] : [['list']],
});
