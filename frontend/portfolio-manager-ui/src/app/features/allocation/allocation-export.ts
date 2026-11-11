import * as XLSX from 'xlsx';
import { CashAccountRow } from './allocation-cash';

export interface AllocationStockRow {
  sector: string;
  industry: string;
  symbol: string;
  company: string;
  marketValue: number;
  weight: number;
}

export interface AllocationOptionRow {
  symbol: string;
  description: string;
  marketValue: number;
  weight: number;
}

export interface AllocationExportData {
  stocks: AllocationStockRow[];
  cash: CashAccountRow[];
  options: AllocationOptionRow[];
}

const HEADER = ['Type', 'Sector', 'Industry', 'Symbol / Account', 'Company / Currency', 'Market Value', 'Portfolio %'];
const MONEY_FORMAT = '#,##0.00;[Red]-#,##0.00';
const PCT_FORMAT = '0.00%';

/** Allocation workbook: numeric cells, one row per cash account, and a separate Cash Total row. */
export function buildAllocationWorkbook(data: AllocationExportData): XLSX.WorkBook {
  const rows: (string | number)[][] = [HEADER];
  for (const s of data.stocks) rows.push(['Stock', s.sector, s.industry, s.symbol, s.company, s.marketValue, s.weight]);
  for (const c of data.cash) rows.push(['Cash', '', '', c.account, c.currency, c.balance, c.weight]);
  if (data.cash.length > 0) {
    rows.push([
      'Cash Total',
      '',
      '',
      '',
      '',
      data.cash.reduce((sum, c) => sum + c.balance, 0),
      data.cash.reduce((sum, c) => sum + c.weight, 0),
    ]);
  }
  for (const o of data.options) rows.push(['Option', '', '', o.symbol, o.description, o.marketValue, o.weight]);

  const sheet = XLSX.utils.aoa_to_sheet(rows);
  for (let r = 1; r < rows.length; r++) {
    const money = sheet[XLSX.utils.encode_cell({ r, c: 5 })];
    const pct = sheet[XLSX.utils.encode_cell({ r, c: 6 })];
    if (money) money.z = MONEY_FORMAT;
    if (pct) pct.z = PCT_FORMAT;
  }
  sheet['!cols'] = [{ wch: 11 }, { wch: 20 }, { wch: 24 }, { wch: 24 }, { wch: 36 }, { wch: 16 }, { wch: 12 }];

  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, 'Allocation');
  return book;
}
