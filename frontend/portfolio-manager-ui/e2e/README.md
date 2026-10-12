# Playwright e2e harness

Regression net for the Angular app across four viewports. **No backend is needed**: every
`/api/**` request is fulfilled in the browser from `fixtures/`.

| Project          | Viewport / device         |
| ---------------- | ------------------------- |
| `desktop-chrome` | Desktop Chrome, 1920×1080 |
| `tablet`         | Chromium, 820×1180, touch |
| `mobile-pixel`   | Pixel 7 (Chromium)        |
| `mobile-iphone`  | iPhone 15 (WebKit)        |

## Run

```powershell
cd frontend\portfolio-manager-ui
npm run e2e                                   # all projects (starts `ng serve` if :4200 is free)
npm run e2e:ui                                # Playwright UI mode
npx playwright test --project=mobile-pixel    # one project
npx playwright test responsive-overflow       # one spec
npm run e2e:report                            # open the last HTML report
npx tsc -p e2e/tsconfig.json                  # type-check the harness
```

Browsers: `npx playwright install chromium webkit` (once). Set `E2E_STUB_FONTS=1` to run fully
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

## Known mobile backlog

`support/known-mobile-backlog.ts` lists `${project}:${route}` overflow failures that exist today.
`responsive-overflow.spec.ts` marks them `test.fail()`, so CI is green now and a fixed page
reports **"expected to fail but passed"** — delete its entry to turn it into a regression guard.
To rebuild the list: empty the set, run `npx playwright test responsive-overflow`, and add every
failing combination. The measured baseline is in `BASELINE.md`.

## Add a route

1. Add an entry to `APP_ROUTES` in `support/routes.ts` (path, nav label, a landmark locator,
   `hasMatTable`, optional `primaryControls`).
2. Mock any new endpoint in `support/mock-api.ts` and add fixtures if the page needs new shapes.
3. Run `npm run e2e`; if the new page overflows on a phone, add its keys to the backlog set.
