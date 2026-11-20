export const CASH_REPORTING_CURRENCY = 'CAD';

export interface CashAccountTotal {
  accountType: string;
  total: number;
}

export interface CashAccountRow {
  account: string;
  /** The cash ledger carries no per-row currency; balances are valued in the Allocation reporting currency. */
  currency: string;
  balance: number;
  /** Fraction (0..1) of the portfolio grand total; 0 when the grand total is not positive. */
  weight: number;
}

/** One row per cash account from the authoritative CashStore totals; never lists ledger transactions. */
export function buildCashAccountRows(totals: readonly CashAccountTotal[], grandTotal: number): CashAccountRow[] {
  return totals.map((t) => ({
    account: t.accountType,
    currency: CASH_REPORTING_CURRENCY,
    balance: t.total,
    weight: grandTotal > 0 ? t.total / grandTotal : 0,
  }));
}
