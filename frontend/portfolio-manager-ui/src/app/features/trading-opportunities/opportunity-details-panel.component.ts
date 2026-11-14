import { DatePipe } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
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
import { fitReasonLabel, reasonLabel, sectorHeatStatusLabel, signedPp } from './opportunities-logic';

const SETUP_LABELS: Record<string, string> = {
  ValidCandidate: 'Valid candidate',
  NotActionable: 'Below actionable reward:risk',
  Unavailable: 'Unavailable',
};

/**
 * PM-029 ticker details (desktop side panel / full-screen on smaller screens). Read-only: it shows the snapshot
 * evidence and every original authoritative Final Action decision; it never computes or changes a decision.
 */
@Component({
  selector: 'app-opportunity-details-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
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

  protected readonly reasonLabel = reasonLabel;
  protected readonly fitReasonLabel = fitReasonLabel;
  protected readonly sectorHeatStatusLabel = sectorHeatStatusLabel;
  protected readonly stateLabel = stateLabel;

  ngAfterViewInit(): void {
    this.closeButton()?.nativeElement.focus();
  }

  protected money(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : this.demo.maskValue(v).toFixed(2);
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

  protected rr(v: number | null | undefined): string {
    return v === null || v === undefined ? '—' : `${v.toFixed(2)} : 1`;
  }

  protected setupLabel(kind: string): string {
    return SETUP_LABELS[kind] ?? kind;
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
