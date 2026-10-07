import { defineConfig, devices } from '@playwright/test';

// Storage UI specs intercept API requests and need no seeded database.
export default defineConfig({
  testDir: './e2e/storage',
  outputDir: './test-results/playwright-storage-mock',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['line']],
  use: {
    baseURL: process.env['E2E_WEB_URL'] ?? 'http://localhost:4200',
    locale: 'ar-SA',
    timezoneId: 'Africa/Cairo',
    serviceWorkers: 'block',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium-desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'chromium-mobile', grep: /@mobile/, use: { ...devices['Pixel 5'] } },
  ],
});
