import { defineConfig, devices } from '@playwright/test';

/**
 * LIVE, READ-ONLY run against a real (HTTPS) Portfolio Manager. Not part of CI and never run automatically.
 *
 * Run it from a terminal (VS Code integrated terminal is fine) while the backend and `ng serve --configuration https`
 * are running. A real browser window opens (headed) and you can watch it work.
 *
 *   cd frontend\portfolio-manager-ui
 *   $env:PM_LIVE_URL = 'https://localhost:4200'      # optional, this is the default
 *   npx playwright test -c playwright.live.config.ts --headed
 *
 * Login: the "setup" project opens the login page and WAITS for you to sign in by hand (up to 5 minutes), then saves
 * the session to `.auth/live.json` (git-ignored). If you prefer, set PM_LIVE_EMAIL / PM_LIVE_PASSWORD and it signs in
 * for you. The "live" project then checks desktop, tablet (800x1280), Samsung S21 (384x854) and S23 (360x780) in ONE
 * shared session by switching the viewport (refresh tokens rotate, so separate sessions would be logged out).
 *
 * The specs only navigate, filter, open dialogs and take screenshots. They never save a plan or a stop and never
 * import anything, so the live portfolio data is not modified.
 */
const BASE_URL = process.env['PM_LIVE_URL'] ?? 'https://localhost:4200';
const SLOW_MO = Number(process.env['PM_LIVE_SLOWMO'] ?? 150);

export default defineConfig({
  testDir: './e2e-live',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 180_000,
  expect: { timeout: 15_000 },
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-live' }]],
  outputDir: 'test-results-live',
  use: {
    baseURL: BASE_URL,
    // The local CA is not in the browser trust store; this run only talks to the URL above.
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    launchOptions: { slowMo: SLOW_MO },
  },
  projects: [
    {
      name: 'setup',
      testMatch: /auth\.setup\.ts/,
      use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } },
    },
    {
      name: 'live',
      testMatch: /\.live\.spec\.ts/,
      dependencies: ['setup'],
      use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } },
    },
  ],
});
