import { test } from './support/test';
import { ALL_ROUTES } from './support/routes';
import { expectNoHorizontalPageOverflow } from './support/overflow';
import { isKnownOverflow } from './support/known-mobile-backlog';

/**
 * Pages must not scroll horizontally at any supported viewport. Combinations listed in
 * `known-mobile-backlog.ts` are expected to fail today (`test.fail`) so CI is green now and a fix
 * surfaces as "expected to fail but passed".
 */
for (const route of ALL_ROUTES) {
  test(`no horizontal overflow: ${route.path}`, async ({ page }, testInfo) => {
    test.fail(
      isKnownOverflow(testInfo.project.name, route.path),
      'Known mobile layout backlog — remove from known-mobile-backlog.ts once fixed',
    );
    await page.goto(route.path);
    await route.landmark(page).waitFor();
    await expectNoHorizontalPageOverflow(page);
  });
}
