import { DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { ScannerRowSkeletonComponent } from '../../shared/skeleton/scanner-row-skeleton.component';
import { AdhocAnalyzerComponent } from './adhoc-analyzer/adhoc-analyzer.component';
import { RsiScannerTableComponent } from './rsi-scanner-table.component';

const MORNING_DISMISSED_KEY = 'morning-check-dismissed';
const MORNING_FORCE_KEY = 'morning-check-force-show';
const MORNING_AUTO_OPENED_KEY = 'morning-check-auto-opened';

@Component({
  selector: 'app-scanner-page',
  templateUrl: './scanner-page.component.html',
  styleUrl: './scanner-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    RsiScannerTableComponent,
    ScannerRowSkeletonComponent,
    AdhocAnalyzerComponent,
  ],
})
export class ScannerPageComponent implements OnInit {
  protected readonly scanner = inject(ScannerStateService);
  private readonly portfolio = inject(PortfolioStateService);
  private readonly watchlist = inject(WatchlistStateService);

  /** Lowercase symbol set for O(1) lookup in the table.
   * Only includes OPEN positions — closed (transactionType === 'CLOSE') are excluded
   * so the Tracking badge is not shown for sold-off tickers. */
  protected readonly portfolioSymbols = computed<ReadonlySet<string>>(
    () =>
      new Set(
        this.portfolio
          .summaries()
          .filter((s) => s.item.transactionType !== 'CLOSE')
          .map((s) => s.item.symbol.toLowerCase()),
      ),
  );

  protected readonly watchlistSymbols = computed<ReadonlySet<string>>(
    () => new Set(this.watchlist.items().map((w) => w.item.symbol.toLowerCase())),
  );

  /** Computed summary for EOD CONFIRM signals found in the current scan. */
  protected readonly eodConfirmSummary = computed(() => {
    const os = this.scanner.eodConfirmOversold().length;
    const ob = this.scanner.eodConfirmOverbought().length;
    const total = os + ob;
    if (total === 0) return null;
    const parts: string[] = [];
    if (os > 0) parts.push(`${os} Oversold`);
    if (ob > 0) parts.push(`${ob} Overbought`);
    return `${total} EOD Confirm signal${total > 1 ? 's' : ''}: ${parts.join(' · ')}`;
  });

  /** Yesterday's persisted EOD CONFIRM signals (for morning check panel). */
  protected readonly yesterdayEod = this.scanner.yesterdayEod;

  /**
   * Whether the morning window is force-enabled via the in-session toggle.
   * Defaults to false on every page load — user must explicitly enable each session.
   */
  protected readonly morningForced = signal(false);

  /**
   * True when there are yesterday EOD signals AND either:
   *  - it is the morning window (server time before noon ET), OR
   *  - the force-show override is enabled.
   */
  protected readonly hasMorningSignals = computed(() => {
    const data = this.yesterdayEod();
    if (!data?.hasData || (data.signals?.length ?? 0) === 0) return false;
    return !!data.isMorningWindow || this.morningForced();
  });

  /** Whether the morning sidebar is currently open. */
  protected readonly morningPanelOpen = signal(false);

  constructor() {
    // Auto-open the sidebar when morning signals arrive, but only ONCE per day
    // (not on every navigation / component re-mount).
    effect(() => {
      if (this.hasMorningSignals() && !this.wasDismissedToday() && !this.wasAutoOpenedToday()) {
        this.morningPanelOpen.set(true);
        this.markAutoOpenedToday();
      }
    });
  }

  private wasAutoOpenedToday(): boolean {
    const stored = localStorage.getItem(MORNING_AUTO_OPENED_KEY);
    const today = new Date().toISOString().split('T')[0];
    return stored === today;
  }

  private markAutoOpenedToday(): void {
    const today = new Date().toISOString().split('T')[0];
    localStorage.setItem(MORNING_AUTO_OPENED_KEY, today);
  }

  private wasDismissedToday(): boolean {
    // Force-show bypasses the dismissed-today check so the override always works
    if (this.morningForced()) return false;
    const dismissed = localStorage.getItem(MORNING_DISMISSED_KEY);
    if (!dismissed) return false;
    const today = new Date().toISOString().split('T')[0];
    return dismissed === today;
  }

  protected openMorningPanel(): void {
    this.morningPanelOpen.set(true);
  }

  protected closeMorningPanel(): void {
    this.morningPanelOpen.set(false);
    if (!this.morningForced()) {
      const today = new Date().toISOString().split('T')[0];
      localStorage.setItem(MORNING_DISMISSED_KEY, today);
    }
  }

  protected toggleMorningForce(): void {
    const next = !this.morningForced();
    this.morningForced.set(next);
    if (next && this.yesterdayEod()?.hasData) {
      // Clear dismissed and auto-opened flags so the panel opens now
      localStorage.removeItem(MORNING_DISMISSED_KEY);
      localStorage.removeItem(MORNING_AUTO_OPENED_KEY);
      this.morningPanelOpen.set(true);
    } else if (!next) {
      this.morningPanelOpen.set(false);
    }
  }

  ngOnInit(): void {
    // No refresh on navigate — the service loads snapshot on init and the Refresh button handles live scans.
  }

  refresh(): void {
    this.scanner.refresh(true);
  }
}
