import { DatePipe } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  model,
  output,
  viewChild,
} from '@angular/core';
import { FormField, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import {
  OpportunityConfig,
  OpportunityDetail,
  OpportunityRow,
  ScorePerspective,
  ScoreResult,
  SectorWindowResult,
} from '../../core/models/opportunities.models';
import { FinalActionResult } from '../../core/models/portfolio.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { FinalActionBadgeComponent } from '../../shared/final-action-badge/final-action-badge.component';
import { FinalActionDetailsDialogComponent } from '../../shared/final-action-details-dialog/final-action-details-dialog.component';
import { stateLabel } from '../../shared/final-action-details-dialog/final-action-labels';
import { ScoreHeatCellComponent } from './cells/score-heat-cell.component';
import { SectorHeatBadgeComponent } from './cells/sector-heat-badge.component';
import {
  companyNameForDisplay,
  EMPTY_MANUAL_R,
  fitReasonLabel,
  ManualRValues,
  manualRewardRisk as calculateManualRewardRisk,
  reasonLabel,
  sectorHeatStatusLabel,
  signedPp,
} from './opportunities-logic';

const SETUP_LABELS: Record<string, string> = {
  ValidCandidate: 'Valid candidate',
  NotActionable: 'Below actionable reward:risk',
  Unavailable: 'Unavailable',
};

/**
 * PM-029 ticker details (desktop side panel / full-screen on smaller screens). Snapshot evidence and authoritative Final Action
 * decisions stay read-only; the only local calculation is an ephemeral Manual R scenario, never used by a decision.
 */
@Component({
  selector: 'app-opportunity-details-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    FormField,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatTooltipModule,
    FinalActionBadgeComponent,
    ScoreHeatCellComponent,
    SectorHeatBadgeComponent,
  ],
  templateUrl: './opportunity-details-panel.component.html',
  styleUrl: './opportunity-details-panel.component.scss',
})
export class OpportunityDetailsPanelComponent implements AfterViewInit {
  private readonly demo = inject(DemoModeStore);
  protected readonly listingFibDetails = (row: OpportunityRow) => {
    const fib = listingFibonacci(row.symbol, row.fibonacciSwing);
    return fib && this.demo.isDemoMode() && this.demo.demoStyle() === 'blur' ? 'Hidden in demo mode'
      : fibonacciDetails(fib, (value) => this.demo.maskValue(value));
  };
  private readonly dialog = inject(MatDialog);

  readonly row = input<OpportunityRow | null>(null);
  readonly detail = input<OpportunityDetail | null>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly perspective = input<ScorePerspective>('Swing');
  readonly config = input<OpportunityConfig | null>(null);
  readonly fullscreen = input(false);
  readonly closed = output<void>();

  private readonly closeButton = viewChild('closeBtn', { read: ElementRef<HTMLButtonElement> });

  /** The detail's own row is snapshot-consistent with its evidence; the grid row is only a placeholder while loading. */
  protected readonly r = computed(() => this.detail()?.row ?? this.row());
  protected readonly decisions = computed(() => this.detail()?.decisions ?? []);
  protected readonly multiple = computed(() => this.r()?.finalAction.kind === 'Multiple');
  protected readonly setup = computed(() => this.detail()?.setup ?? null);
  protected readonly scores = computed<ScoreResult[]>(() => {
    const d = this.detail();
    if (!d) return [];
    return this.perspective() === 'Swing' ? [d.swingScore, d.strategicScore] : [d.strategicScore, d.swingScore];
  });
  protected readonly windows = computed<SectorWindowResult[]>(() => {
    const s = this.detail()?.sectorRelative;
    return s ? [s.d20, s.d60] : [];
  });
  readonly manualRValues = model<ManualRValues>({ ...EMPTY_MANUAL_R });
  protected readonly manualRForm = form(this.manualRValues);
  protected readonly manualRStatus = computed(() => {
    const { entry, stop, target } = this.manualRValues();
    if (entry === null && stop === null && target === null) return 'empty';
    if (entry === null || stop === null || target === null) return 'incomplete';
    return calculateManualRewardRisk({ entry, stop, target }) === null ? 'invalid' : 'valid';
  });
  protected readonly manualRewardRisk = computed(() => {
    return this.manualRStatus() === 'valid' ? calculateManualRewardRisk(this.manualRValues()) : null;
  });
  protected readonly manualRiskPct = computed(() => {
    const { entry, stop } = this.manualRValues();
    return this.manualRStatus() === 'valid' && entry !== null && stop !== null ? ((entry - stop) / entry) * 100 : null;
  });
  protected readonly manualRewardPct = computed(() => {
    const { entry, target } = this.manualRValues();
    return this.manualRStatus() === 'valid' && entry !== null && target !== null ? ((target - entry) / entry) * 100 : null;
  });

  protected readonly reasonLabel = reasonLabel;
  protected readonly fitReasonLabel = fitReasonLabel;
  protected readonly companyName = companyNameForDisplay;
  protected readonly sectorHeatStatusLabel = sectorHeatStatusLabel;
  protected readonly stateLabel = stateLabel;

  ngAfterViewInit(): void {
    this.closeButton()?.nativeElement.focus();
  }

  protected money(v: number | null | undefined, currency: string | null = null): string {
    if (v === null || v === undefined) return '—';
    const formatted = new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(
      this.demo.maskValue(v),
    );
    return currency ? `${currency} ${formatted}` : formatted;
  }

  protected pct(v: number | null | undefined, digits = 1): string {
    return v === null || v === undefined ? '—' : `${this.demo.maskPercent(v).toFixed(digits)}%`;
  }

  protected signedPct(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : `${signedPp(this.demo.maskPercent(v))}%`;
  }

  protected pp(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : `${signedPp(this.demo.maskPercent(v))} pp`;
  }

  protected num(v: number | null | undefined, digits = 1): string {
    return v === null || v === undefined ? '—' : v.toFixed(digits);
  }

  protected points(v: number): string {
    return v.toFixed(2);
  }

  protected rr(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : `${v.toFixed(2)} : 1`;
  }

  protected manualRMessage(): string {
    switch (this.manualRStatus()) {
      case 'valid':
        return 'Session-only scenario; it does not affect Market R:R, scores, Portfolio Fit, or Final Action.';
      case 'invalid':
        return 'For a long scenario, use positive prices with stop below entry and target above entry.';
      default:
        return 'Enter entry, stop, and target prices to calculate a manual reward-to-risk ratio.';
    }
  }

  protected setupLabel(kind: string): string {
    return SETUP_LABELS[kind] ?? kind;
  }

  protected targetSourceLabel(source: string): string {
    switch (source.trim().toLowerCase().replace(/[\s_-]/g, '')) {
      case 'week52high':
      case '52weekhigh':
        return '52-week high';
      case 'upperchannelrail':
        return 'Upper channel rail';
      case 'opengapabove':
        return 'Open gap above';
      default:
        return source.trim();
    }
  }

  protected decisionLabel(d: FinalActionResult): string {
    const parts = [d.source, d.role].filter((p): p is string => !!p);
    return parts.join(' · ');
  }

  protected openDecision(result: FinalActionResult): void {
    this.dialog.open(FinalActionDetailsDialogComponent, { data: { result }, autoFocus: 'dialog', maxWidth: '95vw' });
  }

  protected openSingle(): void {
    const first = this.decisions()[0];
    if (first) this.openDecision(first);
  }
}
import { fibonacciDetails, listingFibonacci } from '../../core/technical-display';
