import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import * as XLSX from 'xlsx';
import { FitStatus, ScorePerspective, SectorHeatDirection } from '../../core/models/opportunities.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import {
  OpportunitiesStore,
  OpportunitySort,
  OpportunityView,
  OwnershipFilter,
  ScoreFilter,
} from '../../core/store/opportunities.store';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import { downloadTextFile } from '../swing-trading/swing-trading-logic';
import { torontoDateString, txRowsToCsv, txSheet } from '../transactions/transactions-logic';
import { OpportunitiesGridComponent, OPPORTUNITIES_GRID_ID } from './opportunities-grid.component';
import {
  ReasonCategory,
  exclusionSummary,
  exportNotes,
  filterRows,
  finalActionFilterLabel,
  opportunityExportColumns,
  opportunityExportRows,
  sortRows,
} from './opportunities-logic';
import { OpportunityDetailsPanelComponent } from './opportunity-details-panel.component';

/** Side panel next to the grid from this width; below it the details open full-screen (plan C.7). */
const SIDE_PANEL_QUERY = '(min-width: 1024px)';

@Component({
  selector: 'app-trading-opportunities-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTooltipModule,
    GridColumnButtonComponent,
    OpportunitiesGridComponent,
    OpportunityDetailsPanelComponent,
  ],
  templateUrl: './trading-opportunities-page.component.html',
  styleUrl: './trading-opportunities-page.component.scss',
  host: { '(document:keydown.escape)': 'onEscape()' },
})
export class TradingOpportunitiesPageComponent {
  protected readonly store = inject(OpportunitiesStore);
  private readonly demo = inject(DemoModeStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly snackBar = inject(MatSnackBar);
  private readonly visibleColumns = inject(GridColumnStore).getColumnKeys(OPPORTUNITIES_GRID_ID);

  protected readonly gridId = OPPORTUNITIES_GRID_ID;
  protected readonly sidePanel = toSignal(
    inject(BreakpointObserver).observe(SIDE_PANEL_QUERY).pipe(map((s) => s.matches)),
    { initialValue: true },
  );
  private readonly querySymbol = toSignal(this.route.queryParamMap.pipe(map((p) => p.get('symbol'))), {
    initialValue: null,
  });

  protected readonly includeAccounts = signal(false);
  protected readonly view = this.store.view;

  private readonly filterInput = computed(() => ({
    view: this.store.view(),
    search: this.store.search(),
    reasonFilter: this.store.reasonFilter(),
    config: this.store.config(),
  }));

  protected readonly visibleRows = computed(() =>
    sortRows(filterRows(this.store.rows(), this.filterInput()), this.store.view().sort, this.store.view().perspective),
  );
  protected readonly summary = computed(() => exclusionSummary(this.store.rows(), this.filterInput()));
  protected readonly panelOpen = computed(() => this.store.selectedSymbol() !== null);

  protected readonly sectorOptions = computed(() => uniqueSorted(this.store.rows().map((r) => r.sector ?? 'Unclassified')));
  protected readonly finalActionOptions = computed(() => uniqueSorted(this.store.rows().map(finalActionFilterLabel)));
  protected readonly sourceOptions = computed(() => uniqueSorted(this.store.rows().flatMap((r) => r.sources)));
  protected readonly fitOptions: { value: FitStatus; label: string }[] = [
    { value: 'Eligible', label: 'Eligible' },
    { value: 'Review', label: 'Review' },
    { value: 'NoAdd', label: 'No Add' },
    { value: 'Unavailable', label: 'Unavailable' },
  ];
  protected readonly heatOptions: { value: SectorHeatDirection; label: string }[] = [
    { value: 'Positive', label: 'Outperforming' },
    { value: 'Neutral', label: 'In line' },
    { value: 'Negative', label: 'Underperforming' },
    { value: 'None', label: 'Unavailable' },
  ];

  /** Threshold labels come from the published config; without it the attractive/moderate filters are disabled. */
  protected readonly scoreFilterOptions = computed(() => {
    const c = this.store.config();
    return [
      { value: 'attractive' as ScoreFilter, label: c ? `Attractive (${c.attractiveMin}+)` : 'Attractive', disabled: !c },
      { value: 'moderate' as ScoreFilter, label: c ? `Moderate (${c.moderateMin}+)` : 'Moderate', disabled: !c },
      { value: 'scored' as ScoreFilter, label: 'All scored', disabled: false },
      { value: 'all' as ScoreFilter, label: 'All (incl. unavailable)', disabled: false },
    ];
  });

  protected readonly hasActiveFilters = computed(() => {
    const v = this.store.view();
    return (
      v.sectors.length + v.fit.length + v.finalActionLabels.length + v.sources.length + v.heat.length > 0 ||
      v.ownership !== 'all' ||
      v.verifiedOnly ||
      v.showUnavailable ||
      this.store.search().trim() !== '' ||
      this.store.reasonFilter() !== null
    );
  });

  constructor() {
    // `?symbol=` is the single source of the open panel, so deep links, reloads and the back button all agree.
    effect(() => {
      const symbol = this.querySymbol();
      untracked(() => this.store.select(symbol ? symbol.toUpperCase() : null));
    });
  }

  protected setPerspective(perspective: ScorePerspective): void {
    this.store.updateView({ perspective });
    this.store.setReasonFilter(null);
  }

  protected update(patch: Partial<OpportunityView>): void {
    this.store.updateView(patch);
  }

  protected setOwnership(value: OwnershipFilter): void {
    this.store.updateView({ ownership: value });
  }

  protected setScoreFilter(value: ScoreFilter): void {
    this.store.updateView({ scoreFilter: value });
    this.store.setReasonFilter(null);
  }

  protected toggleReason(category: ReasonCategory): void {
    this.store.setReasonFilter(this.store.reasonFilter() === category ? null : category);
  }

  protected onSort(sort: OpportunitySort): void {
    this.store.updateView({ sort });
  }

  protected selectSymbol(symbol: string | null): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { symbol },
      queryParamsHandling: 'merge',
      // Full-screen details push a history entry so the phone back button closes them.
      replaceUrl: this.sidePanel(),
    });
  }

  protected closePanel(): void {
    const symbol = this.store.selectedSymbol();
    this.selectSymbol(null);
    if (symbol) {
      // Return focus to the row that opened the panel.
      queueMicrotask(() => document.querySelector<HTMLElement>(`tr[data-symbol="${CSS.escape(symbol)}"]`)?.focus());
    }
  }

  protected onEscape(): void {
    if (this.panelOpen() && !document.querySelector('.cdk-overlay-container .mat-mdc-dialog-container')) {
      this.closePanel();
    }
  }

  protected export(format: 'xlsx' | 'csv', visibleOnly: boolean): void {
    if (this.demo.isDemoMode()) {
      this.snackBar.open('Export is disabled in demo mode.', 'Close', { duration: 4000 });
      return;
    }
    const snapshot = this.store.snapshot();
    if (!snapshot || snapshot.snapshotId === null) return;
    const columns = opportunityExportColumns({
      visibleOnly,
      visibleKeys: this.visibleColumns(),
      includeAccounts: this.includeAccounts(),
    });
    const rows = opportunityExportRows(this.visibleRows(), columns);
    const name = `trading-opportunities-${snapshot.snapshotId}-${torontoDateString()}`;
    if (format === 'csv') {
      downloadTextFile(`${name}.csv`, txRowsToCsv(columns, rows));
      return;
    }
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, txSheet(columns, rows), 'Opportunities');
    XLSX.utils.book_append_sheet(
      book,
      XLSX.utils.aoa_to_sheet(exportNotes(snapshot, this.store.view(), this.summary())),
      'About',
    );
    XLSX.writeFile(book, `${name}.xlsx`);
  }
}

function uniqueSorted(values: readonly string[]): string[] {
  return [...new Set(values)].sort((a, b) => a.localeCompare(b));
}
