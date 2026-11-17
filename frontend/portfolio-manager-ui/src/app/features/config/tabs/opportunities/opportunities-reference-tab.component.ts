import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { IndexConstituent, SectorBenchmarkMapping } from '../../../../core/models/opportunities.models';
import { AuthStore } from '../../../../core/store/auth.store';
import { OpportunityReferenceStore } from '../../../../core/store/opportunity-reference.store';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../../shared/confirm-dialog/confirm-dialog.component';
import { ResponsiveTableDirective } from '../../../../shared/responsive-table/responsive-table.directive';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { BenchmarkDialogComponent } from './benchmark-dialog.component';
import { ConstituentDialogComponent, ConstituentDialogData } from './constituent-dialog.component';
import { parseAliases } from './opportunity-reference-logic';

type StatusFilter = 'current' | 'retired' | 'all';

/**
 * PM-029 reference data: TSX 60 constituents and sector benchmark mappings. Nothing is seeded automatically;
 * an administrator enters each sourced, date-stamped row. Constituents are retired (EffectiveTo), never deleted, and a
 * retire needs a confirmation dialog and can be undone (Restore).
 */
@Component({
  selector: 'app-opportunities-reference-tab',
  templateUrl: './opportunities-reference-tab.component.html',
  styleUrl: './opportunities-reference-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    ResponsiveTableDirective,
    SettingsSectionComponent,
  ],
})
export class OpportunitiesReferenceTabComponent implements OnInit {
  protected readonly store = inject(OpportunityReferenceStore);
  protected readonly isAdmin = inject(AuthStore).isAdmin;
  private readonly dialog = inject(MatDialog);

  protected readonly constituentColumns = computed(() => [
    'symbol', 'companyName', 'effectiveFrom', 'source', ...(this.isAdmin() ? ['actions'] : []),
  ]);
  protected readonly benchmarkColumns = computed(() => [
    'sector', 'market', 'benchmarkSymbol', 'currency', 'aliases', 'source', 'isActive', ...(this.isAdmin() ? ['actions'] : []),
  ]);

  protected readonly status = signal<StatusFilter>('current');
  protected readonly constituentFilter = signal('');
  protected readonly benchmarkFilter = signal('');

  protected readonly constituentSource = new MatTableDataSource<IndexConstituent>([]);
  protected readonly benchmarkSource = new MatTableDataSource<SectorBenchmarkMapping>([]);
  private readonly constituentSort = viewChild<MatSort>('constituentSort');
  private readonly benchmarkSort = viewChild<MatSort>('benchmarkSort');

  protected readonly shownConstituents = computed(() => {
    const all = this.store.constituents();
    const s = this.status();
    return s === 'all' ? all : all.filter((c) => (s === 'retired') === !!c.effectiveTo);
  });

  constructor() {
    this.constituentSource.sortingDataAccessor = (row, column) => String((row as unknown as Record<string, unknown>)[column] ?? '').toLowerCase();
    this.benchmarkSource.sortingDataAccessor = (row, column) =>
      column === 'aliases' ? this.aliases(row).toLowerCase() : String((row as unknown as Record<string, unknown>)[column] ?? '').toLowerCase();
    effect(() => (this.constituentSource.data = this.shownConstituents()));
    effect(() => (this.benchmarkSource.data = this.store.benchmarks()));
    effect(() => (this.constituentSource.filter = this.constituentFilter().trim().toLowerCase()));
    effect(() => (this.benchmarkSource.filter = this.benchmarkFilter().trim().toLowerCase()));
    effect(() => (this.constituentSource.sort = this.constituentSort() ?? null));
    effect(() => (this.benchmarkSource.sort = this.benchmarkSort() ?? null));
  }

  ngOnInit(): void {
    this.store.load();
  }

  protected date(value: string | null | undefined): string {
    return value ? value.substring(0, 10) : '—';
  }

  protected aliases(b: SectorBenchmarkMapping): string {
    return parseAliases(b.providerSectorAliasesJson).join(', ') || '—';
  }

  protected setFilter(kind: 'constituent' | 'benchmark', event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    (kind === 'constituent' ? this.constituentFilter : this.benchmarkFilter).set(value);
  }

  protected addConstituent(): void {
    const last = this.store.constituents().at(-1);
    const data: ConstituentDialogData = {
      effectiveFrom: last?.effectiveFrom?.substring(0, 10) ?? '',
      source: last?.source ?? '',
      sourceAsOfDate: last?.sourceAsOfDate?.substring(0, 10) ?? '',
    };
    this.dialog
      .open(ConstituentDialogComponent, { data, autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((request) => request && this.store.addConstituent(request));
  }

  /** Always asks first; the answer defaults to Cancel. Retiring only end-dates the row and can be undone. */
  protected retire(c: IndexConstituent): void {
    const data: ConfirmDialogData = {
      title: `Retire ${c.symbol}?`,
      message:
        `${c.companyName || c.symbol} will leave the TSX 60 universe on the next refresh of Trading Opportunities. ` +
        'The row is kept (end-dated today) and can be restored at any time.',
      confirmLabel: 'Retire',
      cancelLabel: 'Cancel',
      danger: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((ok) => ok === true && this.store.retireConstituent({ id: c.id, effectiveTo: null }));
  }

  protected restore(c: IndexConstituent): void {
    this.store.restoreConstituent(c.id);
  }

  protected editBenchmark(b: SectorBenchmarkMapping | null): void {
    this.dialog
      .open(BenchmarkDialogComponent, { data: b, autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((request) => request && this.store.upsertBenchmark(request));
  }
}
