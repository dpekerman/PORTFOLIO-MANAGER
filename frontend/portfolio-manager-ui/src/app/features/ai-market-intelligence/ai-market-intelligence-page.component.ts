import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  FeedbackRating,
  GlobalEvent,
  NewsSource,
  NewsSourceCategory,
  NewsSourceRequest,
  PreMarketConfig,
  PreMarketRunSummary,
  StockAudit,
} from '../../core/models/premarket.models';
import { BreakpointStore } from '../../core/store/breakpoint.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { PreMarketStore } from '../../core/store/premarket.store';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import { MobileTableControlsComponent } from '../../shared/responsive-table/mobile-table-controls.component';
import { ResponsiveTableDirective } from '../../shared/responsive-table/responsive-table.directive';
import { dateToTimeString, timeStrToDate } from '../../shared/utils/time';
import { AiDetailDialogComponent, AiDetailDialogData } from './ai-detail-dialog.component';

const EMPTY_SOURCE: NewsSourceRequest = {
  name: '',
  url: '',
  category: 'GLOBAL',
  priority: 50,
  enabled: false,
  markets: 'TSX,US',
  refreshMinutes: 60,
  timeoutSeconds: 10,
  maxItems: 15,
};

const PRIORITY_RANK: Record<string, number> = { HIGH: 3, MEDIUM: 2, LOW: 1, NONE: 0 };
const NEG = Number.NEGATIVE_INFINITY;

type Accessor<T> = (row: T) => string | number;

const SECURITY_SORT: Record<string, Accessor<StockAudit>> = {
  symbol: (a) => a.symbol,
  kind: (a) => a.kind,
  sector: (a) => a.sector,
  shares: (a) => a.shares ?? NEG,
  averageCost: (a) => a.averageCost ?? NEG,
  lastClose: (a) => a.lastClose ?? NEG,
  unrealizedPct: (a) => a.unrealizedPct ?? NEG,
  weightPct: (a) => a.weightPct ?? NEG,
  rsi14: (a) => a.rsi14 ?? NEG,
  change5dPct: (a) => a.change5dPct ?? NEG,
  action: (a) => a.action,
  priority: (a) => PRIORITY_RANK[a.priority] ?? 0,
  change: (a) => a.change ?? '',
  earnings: (a) => a.earningsInDays ?? 999,
  newsStatus: (a) => a.newsStatus,
  rationale: (a) => a.rationale,
};

const SOURCE_SORT: Record<string, Accessor<NewsSource>> = {
  name: (s) => s.name,
  category: (s) => s.category,
  priority: (s) => s.priority,
  status: (s) => s.approvalStatus,
  enabled: (s) => (s.enabled ? 1 : 0),
  lastSuccess: (s) => s.lastSuccessUtc ?? '',
  articles: (s) => s.usefulArticleCount,
  avgMs: (s) => s.avgDurationMs,
  lastError: (s) => s.lastError ?? '',
};

const RUN_SORT: Record<string, Accessor<PreMarketRunSummary>> = {
  asOfDate: (r) => r.asOfDate,
  type: (r) => (r.isTestRun ? 1 : 0),
  status: (r) => r.status,
  emailStatus: (r) => r.emailStatus,
  articles: (r) => r.articlesRetrieved,
  aiCalls: (r) => r.aiCalls,
  durationMs: (r) => r.durationMs,
};

interface SectorGroup {
  sector: string;
  items: StockAudit[];
  holdings: number;
  watch: number;
  high: number;
  medium: number;
  events: GlobalEvent[];
}

const EVENT_SORT: Record<string, Accessor<GlobalEvent>> = {
  category: (e) => e.category,
  title: (e) => e.title,
  exposed: (e) => e.affectedSymbols.length,
  sectors: (e) => e.affectedSectors.join(','),
  evidence: (e) => e.evidence.length,
};

function sortRows<T>(rows: readonly T[], sort: Sort, accessors: Record<string, Accessor<T>>): T[] {
  const get = accessors[sort.active];
  if (!get || !sort.direction) return [...rows];
  const dir = sort.direction === 'asc' ? 1 : -1;
  return [...rows].sort((a, b) => {
    const x = get(a);
    const y = get(b);
    if (typeof x === 'number' && typeof y === 'number') return (x - y) * dir;
    return String(x).localeCompare(String(y)) * dir;
  });
}

function parseIso(s: string | null): Date | null {
  if (!s) return null;
  const [y, m, d] = s.split('-').map(Number);
  return y && m && d ? new Date(y, m - 1, d) : null;
}

function toIso(d: Date | null): string | null {
  return d ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` : null;
}

@Component({
  selector: 'app-ai-market-intelligence-page',
  templateUrl: './ai-market-intelligence-page.component.html',
  styleUrl: './ai-market-intelligence-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    GridColumnButtonComponent,
    NgTemplateOutlet,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSortModule,
    MatTableModule,
    MatTabsModule,
    MatTimepickerModule,
    MatTooltipModule,
    MobileTableControlsComponent,
    ResponsiveTableDirective,
  ],
})
export class AiMarketIntelligencePageComponent {
  protected readonly store = inject(PreMarketStore);
  protected readonly demo = inject(DemoModeStore);
  protected readonly bp = inject(BreakpointStore);
  private readonly dialog = inject(MatDialog);
  private readonly grid = inject(GridColumnStore);

  protected readonly securityCols = this.grid.getResponsiveColumnKeys('ai-securities');
  protected readonly eventCols = this.grid.getResponsiveColumnKeys('ai-events');
  protected readonly sourceCols = this.grid.getResponsiveColumnKeys('ai-sources');
  protected readonly historyCols = this.grid.getResponsiveColumnKeys('ai-history');
  protected readonly evalCols = this.grid.getResponsiveColumnKeys('ai-eval');

  protected readonly categories: NewsSourceCategory[] = ['COMPANY', 'GLOBAL', 'OFFICIAL', 'DISCOVERY'];
  protected readonly priorityOptions = [
    { value: 'ATTENTION', label: 'Needs attention (High + Medium)' },
    { value: 'ALL', label: 'All priorities' },
    { value: 'HIGH', label: 'High' },
    { value: 'MEDIUM', label: 'Medium' },
    { value: 'LOW', label: 'Low' },
  ];
  protected readonly pageSizes = [25, 50, 100];

  // ── Report filters / sort / paging ────────────────────────────────────────
  protected readonly search = signal('');
  protected readonly priorityFilter = signal('ATTENTION');
  protected readonly actionFilter = signal('ALL');
  protected readonly kindFilter = signal('ALL');
  protected readonly sectorFilter = signal('ALL');
  protected readonly selectedStock = signal<StockAudit | null>(null);
  protected readonly selectedEvent = signal<GlobalEvent | null>(null);
  protected readonly changedOnly = signal(false);
  protected readonly newsOnly = signal(false);
  protected readonly earningsSoon = signal(false);
  protected readonly rsiZone = signal('ALL');
  protected readonly sectorOrder = signal<'urgency' | 'name' | 'size'>('urgency');
  protected readonly secSort = signal<Sort>({ active: 'priority', direction: 'desc' });
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(25);
  protected readonly filtersOpen = signal(!this.bp.isHandset());

  protected readonly report = computed(() => this.store.report()?.report ?? null);
  protected readonly events = computed(() => this.report()?.events ?? []);
  protected readonly all = computed<StockAudit[]>(() => {
    const r = this.report();
    return r ? [...r.holdings, ...r.watchlist] : [];
  });
  protected readonly actions = computed(() => [...new Set(this.all().map((a) => a.action))].sort());
  protected readonly sectors = computed(() => [...new Set(this.all().map((a) => a.sector || 'Unclassified'))].sort());
  protected readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const pf = this.priorityFilter();
    const af = this.actionFilter();
    const kf = this.kindFilter();
    const sf = this.sectorFilter();
    const rz = this.rsiZone();
    const changed = this.changedOnly();
    const news = this.newsOnly();
    const earn = this.earningsSoon();
    return this.all().filter(
      (a) =>
        (!q || a.symbol.toLowerCase().includes(q) || a.companyName.toLowerCase().includes(q) || a.sector.toLowerCase().includes(q)) &&
        (pf === 'ALL' || (pf === 'ATTENTION' ? a.priority === 'HIGH' || a.priority === 'MEDIUM' : a.priority === pf)) &&
        (af === 'ALL' || a.action === af) &&
        (kf === 'ALL' || a.kind === kf) &&
        (sf === 'ALL' || (a.sector || 'Unclassified') === sf) &&
        (rz === 'ALL' || (a.rsi14 !== null && (rz === 'OVERSOLD' ? a.rsi14 <= 30 : a.rsi14 >= 70))) &&
        (!changed || (!!a.change && a.change !== 'UNCHANGED')) &&
        (!news || a.news.length > 0) &&
        (!earn || a.earningsInDays !== null),
    );
  });
  protected readonly sortedSecurities = computed(() => sortRows(this.filtered(), this.secSort(), SECURITY_SORT));
  protected readonly pagedSecurities = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.sortedSecurities().slice(start, start + this.pageSize());
  });
  protected readonly activeFilters = computed(
    () =>
      (this.search() ? 1 : 0) +
      (this.priorityFilter() !== 'ATTENTION' ? 1 : 0) +
      (this.actionFilter() !== 'ALL' ? 1 : 0) +
      (this.kindFilter() !== 'ALL' ? 1 : 0) +
      (this.sectorFilter() !== 'ALL' ? 1 : 0) +
      (this.rsiZone() !== 'ALL' ? 1 : 0) +
      (this.changedOnly() ? 1 : 0) +
      (this.newsOnly() ? 1 : 0) +
      (this.earningsSoon() ? 1 : 0),
  );

  /** One entry per sector: the securities that pass the filters, plus the global events that touch the sector. */
  protected readonly sectorGroups = computed<SectorGroup[]>(() => {
    const bySector = new Map<string, StockAudit[]>();
    for (const a of this.filtered()) {
      const key = a.sector || 'Unclassified';
      bySector.set(key, [...(bySector.get(key) ?? []), a]);
    }
    const groups = [...bySector.entries()].map(([sector, items]) => ({
      sector,
      items: sortRows(items, { active: 'priority', direction: 'desc' }, SECURITY_SORT),
      holdings: items.filter((i) => i.kind === 'HOLDING').length,
      watch: items.filter((i) => i.kind === 'WATCHLIST').length,
      high: items.filter((i) => i.priority === 'HIGH').length,
      medium: items.filter((i) => i.priority === 'MEDIUM').length,
      events: this.events().filter((e) => e.affectedSectors.some((s) => sector.toLowerCase().includes(s.toLowerCase()))),
    }));
    const mode = this.sectorOrder();
    return groups.sort((a, b) =>
      mode === 'name'
        ? a.sector.localeCompare(b.sector)
        : mode === 'size'
          ? b.items.length - a.items.length
          : b.high - a.high || b.medium - a.medium || a.sector.localeCompare(b.sector),
    );
  });

  /** Events follow the filters: when a ticker/sector is chosen only events touching those securities remain. */
  protected readonly filteredEvents = computed(() => {
    if (!this.search() && this.sectorFilter() === 'ALL' && this.kindFilter() === 'ALL') return this.events();
    const symbols = new Set(this.filtered().map((a) => a.symbol));
    const sectors = new Set(this.filtered().map((a) => (a.sector || 'Unclassified').toLowerCase()));
    return this.events().filter(
      (e) =>
        e.affectedSymbols.some((s) => symbols.has(s)) ||
        [...sectors].some((s) => e.affectedSectors.some((x) => s.includes(x.toLowerCase()))),
    );
  });
  protected readonly eventSort = signal<Sort>({ active: 'exposed', direction: 'desc' });
  protected readonly sortedEvents = computed(() => sortRows(this.filteredEvents(), this.eventSort(), EVENT_SORT));
  // ── Sources ───────────────────────────────────────────────────────────────
  protected readonly sourceSort = signal<Sort>({ active: 'category', direction: 'asc' });
  protected readonly sortedSources = computed(() => sortRows(this.store.sources(), this.sourceSort(), SOURCE_SORT));
  protected readonly editingId = signal<number | null>(null);
  protected readonly sourceForm = signal<NewsSourceRequest>({ ...EMPTY_SOURCE });
  protected readonly sourcePanelOpen = signal(false);
  protected readonly discoveryReady = computed(() =>
    this.store.sources().some((s) => s.category === 'DISCOVERY' && s.enabled && s.approvalStatus === 'APPROVED'),
  );

  // ── History / evaluation ──────────────────────────────────────────────────
  protected readonly runSort = signal<Sort>({ active: 'asOfDate', direction: 'desc' });
  protected readonly sortedRuns = computed(() => sortRows(this.store.runs(), this.runSort(), RUN_SORT));
  protected readonly evalSort = signal<Sort>({ active: 'ret5', direction: 'desc' });
  protected readonly sortedEval = computed(() => {
    const rows = this.store.evaluation()?.rows ?? [];
    const s = this.evalSort();
    const key = s.active as keyof (typeof rows)[number];
    if (!s.direction) return rows;
    const dir = s.direction === 'asc' ? 1 : -1;
    return [...rows].sort((a, b) => {
      const x = a[key];
      const y = b[key];
      if (typeof x === 'number' || typeof y === 'number') return (((x as number | null) ?? NEG) - ((y as number | null) ?? NEG)) * dir;
      return String(x).localeCompare(String(y)) * dir;
    });
  });

  // ── Agent ─────────────────────────────────────────────────────────────────
  protected readonly runAsOf = signal<Date | null>(null);
  protected readonly runDry = signal(true);
  protected readonly runMock = signal(true);
  protected readonly runEmail = signal(false);
  protected readonly draft = signal<PreMarketConfig | null>(null);

  // Memoised so the pickers get a stable Date instance (a new object per check re-triggers change detection).
  private readonly startStr = computed(() => this.configDraft()?.startTimeLocal ?? null);
  private readonly cutoffStr = computed(() => this.configDraft()?.deadlineLocal ?? null);
  private readonly firstStr = computed(() => this.configDraft()?.firstRunDate ?? null);
  private readonly asOfStr = computed(() => this.configDraft()?.asOfDate ?? null);
  protected readonly startDate = computed(() => timeStrToDate(this.startStr()));
  protected readonly cutoffDate = computed(() => timeStrToDate(this.cutoffStr()));
  protected readonly firstRunDateValue = computed(() => parseIso(this.firstStr()));
  protected readonly asOfDateValue = computed(() => parseIso(this.asOfStr()));

  // ── Report handlers ───────────────────────────────────────────────────────
  protected onSecSort(s: Sort): void {
    this.secSort.set(s);
    this.pageIndex.set(0);
  }

  protected onPage(e: PageEvent): void {
    this.pageIndex.set(e.pageIndex);
    this.pageSize.set(e.pageSize);
  }

  protected setFilter(kind: 'search' | 'priority' | 'action' | 'kind' | 'sector' | 'rsi', value: string): void {
    ({ search: this.search, priority: this.priorityFilter, action: this.actionFilter, kind: this.kindFilter, sector: this.sectorFilter, rsi: this.rsiZone })[kind].set(value);
    this.pageIndex.set(0);
  }

  protected setToggle(kind: 'changed' | 'news' | 'earnings', value: boolean): void {
    ({ changed: this.changedOnly, news: this.newsOnly, earnings: this.earningsSoon })[kind].set(value);
    this.pageIndex.set(0);
  }

  /** Jump from a sector panel to the securities table, limited to that sector. */
  protected pickSector(sector: string): void {
    this.sectorFilter.set(sector);
    this.pageIndex.set(0);
  }

  protected setChanged(v: boolean): void {
    this.changedOnly.set(v);
    this.pageIndex.set(0);
  }

  protected clearFilters(): void {
    this.search.set('');
    this.priorityFilter.set('ATTENTION');
    this.actionFilter.set('ALL');
    this.kindFilter.set('ALL');
    this.sectorFilter.set('ALL');
    this.changedOnly.set(false);
    this.newsOnly.set(false);
    this.earningsSoon.set(false);
    this.rsiZone.set('ALL');
    this.pageIndex.set(0);
  }

  /** Row click shows the details inline (no pop-up needed); the dialog stays available from the card. */
  protected openStock(stock: StockAudit): void {
    this.selectedStock.set(stock);
    setTimeout(() => document.getElementById('selected-security')?.scrollIntoView({ block: 'nearest', behavior: 'smooth' }));
  }

  protected openEvent(event: GlobalEvent): void {
    this.selectedEvent.set(event);
    setTimeout(() => document.getElementById('selected-event')?.scrollIntoView({ block: 'nearest', behavior: 'smooth' }));
  }

  protected openStockDialog(stock: StockAudit): void {
    this.openDetail({ runId: this.store.report()?.id ?? null, stock });
  }

  private openDetail(data: AiDetailDialogData): void {
    this.dialog.open(AiDetailDialogComponent, { data, width: '640px', maxWidth: '96vw', autoFocus: false });
  }

  protected feedback(rating: FeedbackRating, symbol: string | null): void {
    const id = this.store.report()?.id;
    if (id !== undefined) this.store.sendFeedback(id, rating, symbol);
  }
  protected money(v: number | null): string {
    return v === null ? 'n/a' : this.demo.maskValue(v).toFixed(2);
  }

  protected pct(v: number | null): string {
    return v === null ? 'n/a' : `${this.demo.maskPercent(v).toFixed(2)}%`;
  }

  // ── Agent handlers ────────────────────────────────────────────────────────
  protected configDraft(): PreMarketConfig | null {
    return this.draft() ?? this.store.config();
  }

  protected patchDraft(changes: Partial<PreMarketConfig>): void {
    const base = this.configDraft();
    if (base) this.draft.set({ ...base, ...changes });
  }

  protected patchTime(field: 'startTimeLocal' | 'deadlineLocal', value: Date | null): void {
    const next = value ? dateToTimeString(value) : null;
    // valueChange also fires for programmatic changes; ignore echoes of the value we already hold.
    if (next && next !== this.configDraft()?.[field]) this.patchDraft({ [field]: next });
  }

  protected patchDate(field: 'firstRunDate' | 'asOfDate', value: Date | null): void {
    const next = toIso(value);
    if (next !== (this.configDraft()?.[field] ?? null)) this.patchDraft({ [field]: next });
  }

  protected saveConfig(): void {
    const d = this.configDraft();
    if (d) {
      this.store.saveConfig(d);
      this.draft.set(null);
    }
  }

  protected runNow(): void {
    this.store.runNow({
      asOfDate: toIso(this.runAsOf()),
      dryRun: this.runDry(),
      useMockNews: this.runMock(),
      sendEmail: this.runEmail() && !this.runDry(),
    });
  }

  // ── Source handlers ───────────────────────────────────────────────────────
  protected patchSource(changes: Partial<NewsSourceRequest>): void {
    this.sourceForm.update((f) => ({ ...f, ...changes }));
  }

  protected editSource(s: NewsSource): void {
    this.editingId.set(s.id);
    this.sourcePanelOpen.set(true);
    this.sourceForm.set({
      name: s.name,
      url: s.url,
      category: s.category,
      priority: s.priority,
      enabled: s.enabled,
      markets: s.markets,
      refreshMinutes: s.refreshMinutes,
      timeoutSeconds: s.timeoutSeconds,
      maxItems: s.maxItems,
    });
  }

  protected resetSourceForm(): void {
    this.editingId.set(null);
    this.sourceForm.set({ ...EMPTY_SOURCE });
  }

  protected saveSource(): void {
    this.store.saveSource(this.editingId(), this.sourceForm());
    this.resetSourceForm();
    this.sourcePanelOpen.set(false);
  }

  protected toggleSource(s: NewsSource, enabled: boolean): void {
    this.store.saveSource(s.id, { ...s, enabled });
  }

  protected sourceTag(s: NewsSource): string {
    if (s.approvalStatus === 'PENDING') return 'Pending approval';
    return s.enabled ? 'Approved' : 'Disabled';
  }
}
