/**
 * Overflow checks that are KNOWN to fail today (the mobile-first backlog), as
 * `${projectName}:${route}`.
 *
 * `responsive-overflow.spec.ts` marks these with `test.fail()`: CI stays green while the page is
 * broken, and the moment a refactor fixes one, Playwright reports "expected to fail but passed" —
 * remove that entry here to lock the fix in as a regression guard.
 *
 * Regenerate from a fresh baseline with `npx playwright test responsive-overflow` after setting
 * the set to empty, then list every failing combination.
 */
export const KNOWN_OVERFLOW_BACKLOG = new Set<string>([
  'mobile-s21:/dashboard',
  'mobile-s21:/allocation',
  'mobile-s21:/config',
  'mobile-s23:/dashboard',
  'mobile-s23:/allocation',
  'mobile-s23:/config',
]);

export const isKnownOverflow = (project: string, route: string): boolean =>
  KNOWN_OVERFLOW_BACKLOG.has(`${project}:${route}`);
