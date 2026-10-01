import { defineConfig, devices } from '@playwright/test';

const apiUrl = process.env['E2E_API_URL'] ?? 'http://127.0.0.1:5264';
const webUrl = process.env['E2E_WEB_URL'] ?? 'http://127.0.0.1:4200';
const webPort = new URL(webUrl).port || '4200';
const reuseExistingServer = process.env['E2E_REUSE_EXISTING_SERVER'] === '1';

export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  outputDir: './test-results/playwright',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [
    ['line'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }]
  ],
  use: {
    baseURL: webUrl,
    locale: 'ar-SA',
    timezoneId: 'Africa/Cairo',
    serviceWorkers: 'block',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'off'
  },
  projects: [
    {
      name: 'chromium-desktop',
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-mobile',
      grep: /@mobile/,
      use: { ...devices['Pixel 5'] }
    }
  ],
  webServer: [
    {
      command: 'dotnet run --project ../backend/AlFalah.Api/AlFalah.Api.csproj --configuration Release --no-build --no-launch-profile',
      url: `${apiUrl}/swagger/v1/swagger.json`,
      timeout: 240_000,
      reuseExistingServer,
      stdout: 'pipe',
      stderr: 'pipe'
    },
    {
      command: `npm run start -- --host 127.0.0.1 --port ${webPort}`,
      url: webUrl,
      timeout: 180_000,
      reuseExistingServer,
      stdout: 'pipe',
      stderr: 'pipe'
    }
  ]
});
