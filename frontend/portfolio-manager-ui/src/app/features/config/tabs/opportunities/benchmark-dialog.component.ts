import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { SectorBenchmarkMapping, SectorBenchmarkRequest } from '../../../../core/models/opportunities.models';
import { benchmarkFormErrors, parseAliases, splitAliases } from './opportunity-reference-logic';

/** Add a sector benchmark (data = null) or edit an existing one. Closes with the request, or undefined when cancelled. */
@Component({
  selector: 'app-benchmark-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, MatButtonModule, MatCheckboxModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './benchmark-dialog.component.html',
  styleUrl: './reference-dialog.scss',
})
export class BenchmarkDialogComponent {
  private readonly data = inject<SectorBenchmarkMapping | null>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<BenchmarkDialogComponent, SectorBenchmarkRequest | undefined>);

  protected readonly editing = !!this.data;
  protected readonly markets = ['Tsx', 'Nyse'] as const;
  protected readonly model = signal({
    id: this.data?.id ?? (null as number | null),
    sector: this.data?.sector ?? '',
    market: this.data?.market ?? 'Tsx',
    benchmarkSymbol: this.data?.benchmarkSymbol ?? '',
    benchmarkName: this.data?.benchmarkName ?? '',
    currency: this.data?.currency ?? '',
    aliases: this.data ? parseAliases(this.data.providerSectorAliasesJson).join(', ') : '',
    isActive: this.data?.isActive ?? true,
    notes: this.data?.notes ?? '',
    source: this.data?.source ?? '',
    sourceAsOfDate: this.data?.sourceAsOfDate?.substring(0, 10) ?? '',
  });
  protected readonly f = form(this.model);
  protected readonly errors = computed(() => benchmarkFormErrors(this.model()));

  protected submit(): void {
    if (this.errors().length) return;
    const m = this.model();
    this.ref.close({
      id: m.id,
      sector: m.sector.trim(),
      market: m.market,
      benchmarkSymbol: m.benchmarkSymbol.trim(),
      benchmarkName: m.benchmarkName.trim() || null,
      currency: m.currency.trim(),
      providerSectorAliases: splitAliases(m.aliases),
      isActive: m.isActive,
      notes: m.notes.trim() || null,
      source: m.source.trim() || null,
      sourceAsOfDate: m.sourceAsOfDate || null,
    });
  }
}
