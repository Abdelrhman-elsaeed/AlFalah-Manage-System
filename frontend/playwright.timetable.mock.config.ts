import { defineConfig } from '@playwright/test';

// Exercise the real Angular UI with synthetic API responses, without a database.
export default defineConfig({
  testDir: './e2e/timetables',
  outputDir: './test-results/timetable-checkpoints',
  workers: 1,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  reporter: [['line']],
  use: {
    baseURL: 'http://127.0.0.1:4200',
    viewport: { width: 1440, height: 1000 },
    locale: 'ar-SA',
    timezoneId: 'Africa/Cairo',
    serviceWorkers: 'block',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure'
  }
});
