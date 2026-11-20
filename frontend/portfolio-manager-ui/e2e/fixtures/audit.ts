import { isoDateTime } from './factories';

const entry = (
  id: number,
  daysAgo: number,
  over: Record<string, unknown>,
): Record<string, unknown> => ({
  id,
  occurredAtUtc: isoDateTime(-daysAgo, 15),
  userId: 'u-admin',
  userName: 'admin@e2e.test',
  source: 'User',
  entityId: String(id),
  correlationId: null,
  changes: [],
  ...over,
});

export const auditItems = [
  entry(1, 0, {
    category: 'Stock',
    action: 'Update',
    entityType: 'PortfolioItem',
    symbol: 'AAPL',
    accountType: 'TFSA_L_TD',
    summary: 'AAPL: Shares 100 → 120, AverageCostBasis 150 → 152.1',
    changes: [
      { field: 'Shares', old: '100', new: '120' },
      { field: 'AverageCostBasis', old: '150', new: '152.1' },
    ],
  }),
  entry(2, 0, {
    category: 'Option',
    action: 'Update',
    entityType: 'OptionItem',
    symbol: 'MSFT',
    accountType: 'Margin_D_TD',
    summary: 'MSFT: MarketPrice 1.5 → 2.25',
    changes: [{ field: 'MarketPrice', old: '1.5', new: '2.25' }],
  }),
  entry(3, 1, {
    category: 'Cash',
    action: 'Create',
    entityType: 'CashItem',
    accountType: 'TFSA_L_RBC',
    summary: 'Added CashItem: Amount 2500',
    changes: [{ field: 'Amount', old: null, new: '2500' }],
  }),
  entry(4, 2, {
    category: 'Watchlist',
    action: 'Delete',
    entityType: 'WatchlistItem',
    symbol: 'SHOP',
    summary: 'Deleted WatchlistItem SHOP',
    changes: [{ field: 'Symbol', old: 'SHOP', new: null }],
  }),
  entry(5, 2, {
    userId: null,
    userName: null,
    source: 'System',
    category: 'Automation',
    action: 'Other',
    entityType: 'AutomationRunLog',
    summary: 'EOD automation (Scheduled): Success',
  }),
];

export const auditResponse = {
  items: auditItems,
  total: auditItems.length,
  page: 1,
  pageSize: 25,
};

export const auditFilters = {
  categories: ['Stock', 'Option', 'Cash', 'Watchlist', 'Allocation', 'Config', 'User', 'Automation'],
  actions: ['Create', 'Update', 'Delete', 'Restore', 'Adjust', 'Other'],
  users: [{ userId: 'u-admin', userName: 'admin@e2e.test' }],
};
