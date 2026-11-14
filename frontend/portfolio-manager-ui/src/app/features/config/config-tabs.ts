export type ConfigTabId =
  | 'demo'
  | 'scanner'
  | 'eod'
  | 'screener'
  | 'alerts'
  | 'sectors'
  | 'accounts'
  | 'allocation'
  | 'decisions'
  | 'signals'
  | 'history'
  | 'automation'
  | 'users';

export interface ConfigTabDef {
  id: ConfigTabId;
  label: string;
  icon: string;
  adminOnly: boolean;
}

/** Single source of truth for the config tab strip (order = display order). */
export const CONFIG_TABS: readonly ConfigTabDef[] = [
  { id: 'demo', label: 'Demo Mode', icon: 'visibility', adminOnly: false },
  { id: 'scanner', label: 'Scanner', icon: 'tune', adminOnly: false },
  { id: 'eod', label: 'EOD Window', icon: 'schedule', adminOnly: false },
  { id: 'screener', label: 'Value Screener', icon: 'query_stats', adminOnly: false },
  { id: 'alerts', label: 'Alerts', icon: 'notifications_active', adminOnly: false },
  { id: 'sectors', label: 'Sectors', icon: 'category', adminOnly: false },
  { id: 'accounts', label: 'Account Types', icon: 'account_balance', adminOnly: false },
  { id: 'allocation', label: 'Allocation', icon: 'pie_chart', adminOnly: false },
  { id: 'decisions', label: 'Decision Sources', icon: 'label', adminOnly: false },
  { id: 'signals', label: 'Signal Tracker', icon: 'insights', adminOnly: false },
  { id: 'history', label: 'Portfolio History', icon: 'history', adminOnly: true },
  { id: 'automation', label: 'Automation', icon: 'bolt', adminOnly: true },
  { id: 'users', label: 'Users', icon: 'manage_accounts', adminOnly: true },
];
