import { defineConfig, devices } from '@playwright/test';

/**
 * Regression harness for the Angular SPA. The suite never talks to the .NET API: every `/api/**`
 * request is fulfilled in-browser by `e2e/support/mock-api.ts`, so only the dev server is started.
 *
 * Four viewports guard the mobile-first refactor: a desktop baseline, a tablet, and two phones
 * (Chromium/Android and WebKit/iOS) because Safari layout quirks differ from Blink.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  workers: process.env['CI'] ? 2 : undefined,
  reporter: [['list'], ['html', { open: 'never' }]],
  timeout: 45_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'desktop-chrome',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } },
    },
    {
      name: 'tablet',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 820, height: 1180 },
        hasTouch: true,
      },
    },
    { name: 'mobile-pixel', use: { ...devices['Pixel 7'] } },
    { name: 'mobile-iphone', use: { ...devices['iPhone 15'] } },
  ],
  webServer: {
    command: 'npx ng serve --port 4200',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env['CI'],
    timeout: 180_000,
  },
});
