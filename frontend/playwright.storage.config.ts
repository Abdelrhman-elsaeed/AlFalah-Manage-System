import { defineConfig, devices } from '@playwright/test';
// UI contract tests use mocked APIs only; no API startup, SQL seeding or Drive credentials.
export default defineConfig({
  testDir: './e2e/storage', workers: 1, timeout: 45000, reporter: 'list',
  outputDir: './test-results/storage',
  use: { baseURL: 'http://127.0.0.1:4217', locale: 'ar-SA', timezoneId: 'Africa/Cairo', screenshot: 'only-on-failure' },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile', use: { ...devices['Pixel 5'] } }
  ],
  webServer: { command: 'npm start -- --host 127.0.0.1 --port 4217', url: 'http://127.0.0.1:4217', timeout: 180000, reuseExistingServer: true }
});
