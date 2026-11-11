import * as XLSX from 'xlsx';
import { buildCashAccountRows } from './allocation-cash';
import { buildAllocationWorkbook } from './allocation-export';

describe('buildCashAccountRows', () => {
  it('returns one row per account with weights against the grand total', () => {
    const rows = buildCashAccountRows(
      [
        { accountType: 'A', total: 100 },
        { accountType: 'B', total: -50 },
      ],
      1000,
    );
    expect(rows).toEqual([
      { account: 'A', currency: 'CAD', balance: 100, weight: 0.1 },
      { account: 'B', currency: 'CAD', balance: -50, weight: -0.05 },
    ]);
  });

  it('gives zero weight when the grand total is not positive', () => {
    expect(buildCashAccountRows([{ accountType: 'A', total: 5 }], 0)[0].weight).toBe(0);
  });
});

describe('buildAllocationWorkbook', () => {
  it('exports numeric cash balances per account and no ledger descriptions', () => {
    const book = buildAllocationWorkbook({
      stocks: [{ sector: 'Tech', industry: 'Software', symbol: 'AAA', company: 'Alpha', marketValue: 900, weight: 0.9 }],
      cash: [
        { account: 'TFSA', currency: 'CAD', balance: 100, weight: 0.1 },
        { account: 'Margin', currency: 'CAD', balance: -20, weight: -0.02 },
      ],
      options: [],
    });
    const rows = XLSX.utils.sheet_to_json<unknown[]>(book.Sheets['Allocation'], { header: 1 });
    const cashRows = rows.filter((r) => r[0] === 'Cash');
    expect(cashRows.length).toBe(2);
    expect(cashRows.map((r) => r[3])).toEqual(['TFSA', 'Margin']);
    expect(typeof cashRows[0][5]).toBe('number');
    expect(cashRows[1][5]).toBe(-20);
    const total = rows.find((r) => r[0] === 'Cash Total');
    expect(total?.[5]).toBe(80);
    expect(JSON.stringify(rows)).not.toContain('Opening Balance');
  });
});
