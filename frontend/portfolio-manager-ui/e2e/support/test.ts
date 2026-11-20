import { test as base, expect } from '@playwright/test';
import { installApiMocks, type ApiMocks } from './mock-api';

/**
 * Console noise that is unrelated to app health:
 * - favicon requests (dev server serves none for some browsers),
 * - the browser's generic "Failed to load resource" line, which only mirrors a network failure
 *   already visible elsewhere (mocked endpoints never fail),
 * - Google font fetches when the suite runs offline (see `installApiMocks`).
 */
const IGNORED_CONSOLE_ERRORS: RegExp[] = [
  /favicon/i,
  /Failed to load resource/i,
  /fonts\.(googleapis|gstatic)\.com/i,
];

export interface AppFixtures {
  /** Handle to the installed API mocks (unmocked request log). */
  mocks: ApiMocks;
  /** Uncaught page exceptions and `console.error` messages collected since the test started. */
  errors: string[];
}

/**
 * `test` with `/api/**` mocked and runtime errors collected. Specs assert `errors` is empty
 * so a template crash or a failing subscription never goes unnoticed.
 */
export const test = base.extend<AppFixtures>({
  // `auto` so mocks/collectors are active even for specs that never name the fixtures:
  // an un-mocked page would silently proxy to a backend that is not running.
  mocks: [
    async ({ page }, use) => {
      await use(await installApiMocks(page));
    },
    { auto: true },
  ],
  errors: [
    async ({ page, mocks }, use) => {
      void mocks; // ensure mocks are registered before the first navigation
      const errors: string[] = [];
      page.on('pageerror', (err) => errors.push(`pageerror: ${err.message}`));
      page.on('console', (msg) => {
        if (msg.type() !== 'error') return;
        const text = msg.text();
        if (IGNORED_CONSOLE_ERRORS.some((re) => re.test(text))) return;
        errors.push(`console.error: ${text}`);
      });
      await use(errors);
    },
    { auto: true },
  ],
});

export { expect };
