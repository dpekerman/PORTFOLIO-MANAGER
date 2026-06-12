import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import * as XLSX from 'xlsx';
import { PortfolioStateService } from '../../../core/services/portfolio-state.service';

interface ImportRow {
  symbol: string;
  companyName: string;
  shares: number;
  avgCost: number;
  status: 'pending' | 'importing' | 'done' | 'error';
  error?: string;
}

@Component({
  selector: 'app-import-stocks-dialog',
  templateUrl: './import-stocks-dialog.component.html',
  styleUrl: './import-stocks-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTooltipModule,
    MatChipsModule,
    NgClass,
  ],
})
export class ImportStocksDialogComponent {
  private readonly state = inject(PortfolioStateService);
  private readonly snackBar = inject(MatSnackBar);
  readonly dialogRef = inject(MatDialogRef<ImportStocksDialogComponent>);

  protected readonly rows = signal<ImportRow[]>([]);
  protected readonly importing = signal(false);
  protected readonly progress = signal(0);
  protected readonly fileName = signal<string | null>(null);
  protected readonly parseError = signal<string | null>(null);

  readonly displayedColumns = ['symbol', 'companyName', 'shares', 'avgCost', 'status'];

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.fileName.set(file.name);
    this.parseError.set(null);
    this.rows.set([]);

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const wb = XLSX.read(e.target!.result, { type: 'array' });
        const ws = wb.Sheets[wb.SheetNames[0]];
        const raw = XLSX.utils.sheet_to_json<Record<string, unknown>>(ws, { defval: '' });

        if (raw.length === 0) {
          this.parseError.set('The spreadsheet appears to be empty.');
          return;
        }

        const parsed = raw
          .map((row) => {
            // Case-insensitive column lookup
            const get = (keys: string[]): string => {
              for (const k of Object.keys(row)) {
                if (keys.some((key) => k.toLowerCase().includes(key.toLowerCase()))) {
                  return String(row[k] ?? '').trim();
                }
              }
              return '';
            };

            const symbol = get(['symbol', 'ticker', 'sym']).toUpperCase();
            const companyName = get(['company', 'name', 'description']) || symbol;
            const sharesRaw = get(['shares', 'qty', 'quantity', 'units']);
            const costRaw = get(['cost', 'price', 'avg', 'average', 'basis']);

            const shares = parseFloat(sharesRaw) || 1;
            const avgCost = parseFloat(costRaw) || 0.01;

            return { symbol, companyName, shares, avgCost, status: 'pending' as const };
          })
          .filter((r) => r.symbol.length > 0 && r.symbol.length <= 15);

        if (parsed.length === 0) {
          this.parseError.set(
            'No valid rows found. Make sure the file has a "Symbol" column. ' +
              'Optional columns: Company, Shares, AvgCost.',
          );
          return;
        }

        this.rows.set(parsed);
      } catch (err) {
        this.parseError.set('Could not read the file. Please upload a valid .xlsx or .csv file.');
      }
    };
    reader.readAsArrayBuffer(file);
  }

  async importAll(): Promise<void> {
    const rows = this.rows();
    if (rows.length === 0) return;

    this.importing.set(true);
    let done = 0;

    for (let i = 0; i < rows.length; i++) {
      const row = rows[i];
      this.rows.update((r) => r.map((x, idx) => (idx === i ? { ...x, status: 'importing' } : x)));

      try {
        await this.state.addItem({
          symbol: row.symbol,
          companyName: row.companyName,
          shares: row.shares,
          averageCostBasis: row.avgCost,
        });
        this.rows.update((r) => r.map((x, idx) => (idx === i ? { ...x, status: 'done' } : x)));
      } catch (e: unknown) {
        const msg = e instanceof Error ? e.message : 'Failed';
        this.rows.update((r) =>
          r.map((x, idx) => (idx === i ? { ...x, status: 'error', error: msg } : x)),
        );
      }

      done++;
      this.progress.set(Math.round((done / rows.length) * 100));
    }

    this.importing.set(false);
    const succeeded = this.rows().filter((r) => r.status === 'done').length;
    this.snackBar.open(`Imported ${succeeded} of ${rows.length} stocks`, 'Close', {
      duration: 4000,
    });

    if (succeeded > 0) this.dialogRef.close(true);
  }

  downloadTemplate(): void {
    const ws = XLSX.utils.aoa_to_sheet([
      ['Symbol', 'Company', 'Shares', 'AvgCost'],
      ['RY.TO', 'Royal Bank of Canada', 10, 125.0],
      ['TD.TO', 'TD Bank', 15, 85.5],
      ['SHOP.TO', 'Shopify', 5, 95.0],
    ]);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Portfolio');
    XLSX.writeFile(wb, 'portfolio-template.xlsx');
  }
}
