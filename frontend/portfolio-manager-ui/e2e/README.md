# Playwright e2e harness

Regression net for the Angular app across four viewports (the only supported devices: Samsung S21
Ultra, Samsung S23, Pixel Tablet and desktop; design for a 360px minimum width). **No backend is
needed**: every `/api/**` request is fulfilled in the browser from `fixtures/`.

| Project          | Viewport / device                           |
| ---------------- | ------------------------------------------- |
| `desktop-chrome` | Desktop Chrome, 1920×1080                   |
| `tablet`         | Pixel Tablet portrait, 800×1280, touch      |
| `mobile-s21`     | Samsung S21 Ultra, 384×854 (Chromium)       |
| `mobile-s23`     | Samsung S23, 360×780 (Chromium)             |

## Run

```powershell
cd frontend\portfolio-manager-ui
npm run e2e                                   # all projects (starts `ng serve` if :4200 is free)
npm run e2e:ui                                # Playwright UI mode
npx playwright test --project=mobile-s23      # one project
npx playwright test responsive-overflow       # one spec
npm run e2e:report                            # open the last HTML report
npx tsc -p e2e/tsconfig.json                  # type-check the harness
```

Browsers: `npx playwright install chromium` (once). Set `E2E_STUB_FONTS=1` to run fully
offline; icons then render as ligature text, so overflow results are not representative.

## How the mocks work

- `support/test.ts` exports the `test` to use in every spec. Its auto fixtures call
  `installApiMocks(page)` and collect `pageerror` / `console.error` into `errors`.
- `support/mock-api.ts` holds an ordered table of `get/on(method, /path/, handler)` entries with
  realistic data from `fixtures/`. Unknown GETs return `[]`, DELETEs 204, other writes `{}`; the
  fallbacks are listed in `mocks.unmocked`, so a page hitting a new endpoint is visible in a spec
  (`console.log(mocks.unmocked)`).
- Auth is mocked as an Admin session (`/auth/setup-required` → `{required:false}`,
  `/auth/refresh` → token + user) so `authGuard` passes and write controls render.
- Fixtures build on `fixtures/factories.ts`. Keep tables populated with several rows and every
  column filled: empty tables make overflow checks meaningless, and a missing nested object
  crashes the template.

## Dashboard on small screens

`dashboard-mobile.spec.ts` expands every dashboard section (market leadership starts collapsed) on the
phone and tablet projects and asserts no page overflow, no card scrolling sideways internally, and
that the dense tables render inside the viewport; on phones also that they are grid cards. It skips
`desktop-chrome`.

## Known mobile backlog

`support/known-mobile-backlog.ts` lists `${project}:${route}` overflow failures that exist today
(currently **empty**: every route fits 360px and 384px).
`responsive-overflow.spec.ts` marks them `test.fail()`, so CI is green now and a fixed page
reports **"expected to fail but passed"** — delete its entry to turn it into a regression guard.
To rebuild the list: empty the set, run `npx playwright test responsive-overflow`, and add every
failing combination. The measured baseline is in `BASELINE.md`.

## Add a route

1. Add an entry to `APP_ROUTES` in `support/routes.ts` (path, nav label, a landmark locator,
   `hasMatTable`, optional `primaryControls`).
2. Mock any new endpoint in `support/mock-api.ts` and add fixtures if the page needs new shapes.
3. Run `npm run e2e`; if the new page overflows on a phone, add its keys to the backlog set.
