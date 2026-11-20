import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OpportunityRow, ScorePerspective } from '../../core/models/opportunities.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { GRID_REGISTRY } from '../../core/store/grid-registry';
import { OpportunitySort } from '../../core/store/opportunities.store';
import { FinalActionBadgeComponent } from '../../shared/final-action-badge/final-action-badge.component';
import { MobileTableControlsComponent } from '../../shared/responsive-table/mobile-table-controls.component';
import { ResponsiveTableDirective } from '../../shared/responsive-table/responsive-table.directive';
import { ROW_SELECTION, RowSelectionDirective } from '../../shared/row-selection/row-selection.directive';
import { ScoreHeatCellComponent } from './cells/score-heat-cell.component';
import { SectorHeatBadgeComponent } from './cells/sector-heat-badge.component';
import {
  columnValue,
  companyNameForDisplay,
  fitReasonLabel,
  ManualRValues,
  manualRewardRisk,
  manualRScenarioKey,
  marketRFor,
  reasonLabel,
  setupQualityFor,
  signedPp,
  tableSortKey,
} from './opportunities-logic';

export const OPPORTUNITIES_GRID_ID = 'trading-opportunities';

type SimpleKind = 'money' | 'pct' | 'pp' | 'num1' | 'num2' | 'int' | 'text';

interface SimpleColumn {
  key: string;
  label: string;
  kind: SimpleKind;
}

/** Columns rendered by one generic template; the rest have bespoke cells in the template. */
const SIMPLE_KINDS: Record<string, SimpleKind> = {
  price: 'money',
  rsi14: 'num1',
  macdHist: 'num2',
  ema20: 'money',
  sma50: 'money',
  sma200: 'money',
  volumeRatio: 'num2',
  rs20: 'pp',
  rs60: 'pp',
  stock20d: 'pct',
  bench20d: 'pct',
  entry: 'money',
  stop: 'money',
  target: 'money',
  riskPct: 'pct',
  rewardPct: 'pct',
  setupKind: 'text',
  owned: 'text',
  sources: 'text',
  weightPct: 'pct',
  sectorWeightPct: 'pct',
  positionHeadroom: 'pct',
  sectorHeadroom: 'pct',
  accounts: 'text',
  roles: 'text',
  industry: 'text',
  swingCompleteness: 'int',
  strategicCompleteness: 'int',
  dataAsOf: 'text',
  status: 'text',
};

const GRID_DEF = GRID_REGISTRY.find((g) => g.id === OPPORTUNITIES_GRID_ID)!;
const LABELS = new Map(GRID_DEF.columns.map((c) => [c.key, c.label]));
const SIMPLE_COLUMNS: SimpleColumn[] = Object.entries(SIMPLE_KINDS).map(([key, kind]) => ({
  key,
  kind,
  label: LABELS.get(key) ?? key,
}));

const FIT_LABELS: Record<string, string> = { Eligible: 'Eligible', Review: 'Review', NoAdd: 'No Add', Unavailable: '—' };

/**
 * PM-029 opportunity heat grid. Presentation only: rows arrive filtered and sorted from the page; every value is
 * read from the snapshot row. Final Action is the authoritative decision (or a neutral "Multiple (n)").
 */
@Component({
  selector: 'app-opportunities-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ...ROW_SELECTION,
    MatTableModule,
    MatSortModule,
    MatIconModule,
    MatTooltipModule,
    ResponsiveTableDirective,
    MobileTableControlsComponent,
    FinalActionBadgeComponent,
    ScoreHeatCellComponent,
    SectorHeatBadgeComponent,
  ],
  templateUrl: './opportunities-grid.component.html',
  styleUrl: './opportunities-grid.component.scss',
})
export class OpportunitiesGridComponent {
  private readonly demo = inject(DemoModeStore);
  protected readonly demoBlur = () => this.demo.isDemoMode() && this.demo.demoStyle() === 'blur';
  protected readonly fibFor = (row: OpportunityRow) => listingFibonacci(row.symbol, row.fibonacciSwing);
  protected readonly fibRange = (row: OpportunityRow) =>
    fibonacciRange(this.fibFor(row), (value) => this.demo.maskValue(value));
  protected readonly fibDetails = (row: OpportunityRow) =>
    this.demoBlur() ? '' : fibonacciDetails(this.fibFor(row), (value) => this.demo.maskValue(value));
  private readonly gridColumns = inject(GridColumnStore);

  readonly rows = input.required<OpportunityRow[]>();
  readonly perspective = input.required<ScorePerspective>();
  readonly sort = input.required<OpportunitySort>();
  readonly manualRByScenario = input<Readonly<Record<string, ManualRValues>>>({});
  readonly selectedSymbol = input<string | null>(null);
  readonly emptyMessage = input('No securities match the current filters.');
  readonly emptyActionLabel = input<string | null>(null);
  readonly rowSelected = output<string | null>();
  readonly sortChanged = output<OpportunitySort>();
  readonly emptyAction = output<void>();

  protected readonly gridId = OPPORTUNITIES_GRID_ID;
  protected readonly columns = computed(() =>
    this.gridColumns.getResponsiveColumnKeys(OPPORTUNITIES_GRID_ID, this.perspective())(),
  );
  protected readonly simpleColumns = SIMPLE_COLUMNS;
  protected readonly label = (key: string) => LABELS.get(key) ?? key;
  protected readonly companyName = companyNameForDisplay;
  protected readonly sortActive = computed(() => tableSortKey(this.sort(), this.perspective()));

  private readonly selection = viewChild(RowSelectionDirective);

  constructor() {
    // The store owns the selection (deep links, back button, panel close); the row highlight follows it.
    effect(() => {
      const symbol = this.selectedSymbol();
      const sel = this.selection();
      if (!sel) return;
      if (symbol) sel.select(symbol);
      else sel.clear();
    });
  }

  protected onSort(sort: Sort): void {
    const perspectiveKey = this.perspective().toLowerCase();
    const active = sort.active === perspectiveKey ? 'score' : sort.active;
    this.sortChanged.emit({ active, direction: sort.direction });
  }

  protected toggle(row: OpportunityRow): void {
    this.rowSelected.emit(this.selectedSymbol() === row.symbol ? null : row.symbol);
  }

  protected open(row: OpportunityRow): void {
    this.rowSelected.emit(row.symbol);
  }

  protected onRowKey(event: KeyboardEvent, row: OpportunityRow): void {
    const tr = event.currentTarget as HTMLElement;
    switch (event.key) {
      case 'Enter':
      case ' ':
        if ((event.target as HTMLElement) !== tr) return;
        event.preventDefault();
        this.open(row);
        break;
      case 'ArrowDown':
        event.preventDefault();
        (tr.nextElementSibling as HTMLElement | null)?.focus();
        break;
      case 'ArrowUp':
        event.preventDefault();
        (tr.previousElementSibling as HTMLElement | null)?.focus();
        break;
    }
  }

  protected simpleText(row: OpportunityRow, col: SimpleColumn): string {
    const v = columnValue(row, col.key, this.perspective());
    if (v === null || v === undefined || v === '') return '—';
    if (typeof v === 'string') return v;
    switch (col.kind) {
      case 'money':
        return this.demo.maskValue(v).toFixed(2);
      case 'pct':
        return `${this.demo.maskPercent(v).toFixed(1)}%`;
      case 'pp':
        return `${signedPp(this.demo.maskPercent(v))} pp`;
      case 'num1':
        return v.toFixed(1);
      case 'num2':
        return v.toFixed(2);
      case 'int':
        return `${Math.round(v)}%`;
      default:
        return String(v);
    }
  }

  protected rrText(row: OpportunityRow): string {
    const rr = marketRFor(row, this.perspective());
    return rr === null ? 'N/A' : `${rr.toFixed(1)} : 1`;
  }

  protected quality(row: OpportunityRow) {
    return setupQualityFor(row, this.perspective());
  }

  /** 'valid' | 'weak' | 'na' for the Market R chip in either perspective. */
  protected rrState(row: OpportunityRow): 'valid' | 'weak' | 'na' {
    if (this.perspective() === 'Strategic') {
      const q = row.strategicSetupQuality;
      if (q?.marketRewardRisk == null) return 'na';
      return q.status === 'Poor' ? 'weak' : 'valid';
    }
    return row.setup.kind === 'ValidCandidate' ? 'valid' : row.setup.kind === 'NotActionable' ? 'weak' : 'na';
  }

  protected qualityTooltip(row: OpportunityRow): string {
    const q = this.quality(row);
    const reasons = q?.reasonCodes?.map(reasonLabel).join('; ');
    if (this.perspective() === 'Strategic')
      return reasons
        ? `Strategic setup (confirmed support → resistance): ${reasons}`
        : 'Strategic setup: confirmed support as invalidation level, confirmed resistance as target; no signal stop needed.';
    return reasons || 'Structural snapshot evidence unavailable.';
  }

  protected rrTooltip(row: OpportunityRow): string {
    if (this.perspective() === 'Strategic') {
      const q = row.strategicSetupQuality;
      if (q?.marketRewardRisk != null)
        return 'Strategic Market R: distance to confirmed resistance divided by distance to confirmed support. No signal stop is used.';
      const missing = q?.reasonCodes?.map(reasonLabel).join('; ');
      return `No confirmed structure${missing ? `: ${missing}` : ''}`;
    }
    const s = row.setup;
    if (s.kind === 'ValidCandidate') return 'Market R from the calculated candidate setup; not your saved risk plan.';
    const reasons = s.reasonCodes.map(reasonLabel).join('; ');
    if (s.kind === 'NotActionable') return `Calculated, but below the actionable reward:risk${reasons ? ` · ${reasons}` : ''}`;
    return `No verifiable setup${reasons ? `: ${reasons}` : ''}`;
  }

  protected manualRText(row: OpportunityRow): string {
    const rr = manualRewardRisk(
      this.manualRByScenario()[manualRScenarioKey(row.symbol, this.perspective())],
    );
    return rr === null ? '—' : `${rr.toFixed(2)} : 1`;
  }

  protected fitLabel(row: OpportunityRow): string {
    return FIT_LABELS[row.fit.status] ?? row.fit.status;
  }

  protected fitTooltip(row: OpportunityRow): string {
    return row.fit.reasonCodes.length
      ? row.fit.reasonCodes.map(fitReasonLabel).join('; ')
      : `Portfolio fit: ${this.fitLabel(row)}`;
  }

  protected finalActionLabel(row: OpportunityRow): string {
    const fa = row.finalAction;
    if (fa.kind === 'Multiple') return `Multiple (${fa.decisionCount})`;
    return fa.label ?? '';
  }

  protected finalActionTooltip(row: OpportunityRow): string {
    const fa = row.finalAction;
    if (fa.kind === 'Multiple') {
      return `Account/lot decisions differ: ${fa.distinctLabels.join(' · ')}. Open the details to see each original decision.`;
    }
    return fa.reason ?? fa.label ?? '';
  }
}
import { fibonacciDetails, fibonacciRange, listingFibonacci } from '../../core/technical-display';
