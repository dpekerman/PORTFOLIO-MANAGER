import type { Locator, Page } from '@playwright/test';

/**
 * Single source of truth for the routes under test. To cover a new page, add one entry here —
 * every spec iterates this list.
 */
export interface RouteSpec {
  /** Router path. */
  path: string;
  /** Sidenav/toolbar label (only for routes in the main navigation). */
  navLabel?: string;
  /** Locates the page's own title/landmark; resolved after navigation. */
  landmark: (page: Page) => Locator;
  /** True when the page renders a Material data table (`table.mat-mdc-table`). */
  hasMatTable: boolean;
  /** Primary actions/filters a phone user must be able to reach without horizontal panning. */
  primaryControls?: (page: Page) => Locator[];
  /** Shell routes sit inside the authenticated layout (toolbar + sidenav). */
  inLayout: boolean;
}

/** Matches by visible text or accessible name � icon buttons often override the text with an aria-label. */
const btn = (page: Page, name: string | RegExp) =>
  page.locator('button').filter({ hasText: name }).or(page.getByRole('button', { name }));

const heading = (level: 1 | 2, name: RegExp) => (page: Page) =>
  page.getByRole('heading', { level, name }).first();

export const LOGIN_ROUTE: RouteSpec = {
  path: '/login',
  landmark: (page) => page.getByText('Sign in to your account'),
  hasMatTable: false,
  inLayout: false,
};

/** The ten authenticated routes, in navigation order. */
export const APP_ROUTES: RouteSpec[] = [
  {
    path: '/dashboard',
    navLabel: 'Dashboard',
    landmark: heading(1, /^Dashboard$/),
    hasMatTable: false,
    inLayout: true,
  },
  {
    path: '/portfolio',
    navLabel: 'Portfolio',
    landmark: heading(2, /MY PORTFOLIO/),
    hasMatTable: true,
    primaryControls: (p) => [btn(p, /Add Stock/), btn(p, 'Refresh portfolio prices')],
    inLayout: true,
  },
  {
    path: '/watchlist',
    navLabel: 'Watchlist',
    landmark: heading(1, /MY WATCHLIST/),
    hasMatTable: true,
    primaryControls: (p) => [btn(p, /Add Symbol/), p.getByPlaceholder(/Filter by ticker/)],
    inLayout: true,
  },
  {
    path: '/scanner',
    navLabel: 'RSI Scanner',
    landmark: heading(2, /OVERSOLD CHAIN/),
    hasMatTable: true,
    primaryControls: (p) => [p.getByPlaceholder('Type symbol and press Enter'), btn(p, /Analyze/)],
    inLayout: true,
  },
  {
    path: '/eod-signals',
    navLabel: 'EOD Signals',
    landmark: heading(1, /EOD SIGNALS/),
    hasMatTable: true,
    primaryControls: (p) => [p.getByPlaceholder('e.g. TD.TO'), btn(p, /Delete All/)],
    inLayout: true,
  },
  {
    path: '/transactions',
    navLabel: 'Transactions',
    landmark: heading(2, /TRANSACTIONS/),
    hasMatTable: true,
    primaryControls: (p) => [btn(p, /^Open$/), btn(p, /Export CSV/)],
    inLayout: true,
  },
  {
    path: '/allocation',
    navLabel: 'Allocation',
    landmark: heading(1, /PORTFOLIO ALLOCATION/),
    hasMatTable: false,
    inLayout: true,
  },
  {
    path: '/value-screener',
    navLabel: 'Value Screener',
    landmark: heading(2, /VALUE SCREENER/),
    hasMatTable: true,
    primaryControls: (p) => [btn(p, /Clear Data/), btn(p, /Export CSV/)],
    inLayout: true,
  },
  {
    path: '/portfolio-value-history',
    navLabel: 'Value History',
    landmark: heading(1, /Portfolio Value History/),
    hasMatTable: true,
    primaryControls: (p) => [btn(p, /Reset Filters/)],
    inLayout: true,
  },
  {
    path: '/audit',
    navLabel: 'Audit',
    landmark: heading(1, /^Audit$/),
    hasMatTable: true,
    primaryControls: (p) => [btn(p, /Export CSV/)],
    inLayout: true,
  },
  {
    path: '/config',
    navLabel: 'Configuration',
    landmark: heading(2, /Scanner Settings/),
    hasMatTable: false,
    inLayout: true,
  },
];

/** Every route including the public login page. */
export const ALL_ROUTES: RouteSpec[] = [...APP_ROUTES, LOGIN_ROUTE];
