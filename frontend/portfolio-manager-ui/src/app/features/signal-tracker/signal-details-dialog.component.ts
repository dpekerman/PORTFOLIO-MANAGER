import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SignalHorizon } from '../../core/models/portfolio.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { SignalsAuditStore } from '../../core/store/signals-audit.store';

export interface SignalDetailsDialogData {
  id: number;
}

/** Read-only view of a signal exactly as it was captured, plus how it has played out since. */
@Component({
  selector: 'app-signal-details-dialog',
  templateUrl: './signal-details-dialog.component.html',
  styleUrl: './signal-details-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, MatButtonModule, MatDialogModule, MatIconModule, MatProgressBarModule],
})
export class SignalDetailsDialogComponent {
  private readonly data = inject<SignalDetailsDialogData>(MAT_DIALOG_DATA);
  protected readonly store = inject(SignalsAuditStore);
  protected readonly demoMode = inject(DemoModeStore);

  /** Only show detail that belongs to this dialog; the store holds one detail at a time. */
  protected readonly detail = computed(() => {
    const d = this.store.detail();
    return d && d.row.id === this.data.id ? d : null;
  });

  protected readonly context = computed(() => this.detail()?.context ?? null);

  constructor() {
    this.store.openDetail(this.data.id);
  }

  protected price(value: number): number {
    return this.demoMode.maskValue(value);
  }

  protected pct(h: SignalHorizon): number {
    return this.demoMode.maskPercent(h.pct ?? 0);
  }

  /** Stringifies a captured structure value; anything missing reads "Not Available". */
  protected text(value: unknown): string {
    if (value === null || value === undefined || value === '') return 'Not Available';
    return typeof value === 'object' ? JSON.stringify(value) : String(value);
  }

  /** Price Structure fields exactly as captured (camelCase JSON); absent when not stored. */
  private structure(key: string): unknown {
    const ps = this.context()?.technicalFacts?.priceStructure as Record<string, unknown> | null | undefined;
    return ps?.[key];
  }

  protected priceStructureText(): string {
    return this.text(this.structure('label'));
  }

  protected keyLevelText(): string {
    const price = this.structure('keyLevelPrice');
    if (typeof price !== 'number') return 'Not Available';
    const role = this.structure('keyLevelRole');
    const type = this.structure('keyLevelType');
    const label = [type, role].filter((v) => typeof v === 'string' && v).join(' ');
    return `${label} ${this.price(price).toFixed(2)}`.trim();
  }

  protected channelText(): string {
    const upper = this.structure('upperTrendline');
    const lower = this.structure('lowerTrendline');
    if (typeof upper !== 'number' || typeof lower !== 'number') return 'Not Available';
    return `${this.price(lower).toFixed(2)} – ${this.price(upper).toFixed(2)}`;
  }

  protected fibonacciText(): string {
    const fib = this.context()?.fibonacci as { zone?: string; status?: string; level618?: number } | null | undefined;
    if (!fib) return 'Not Available';
    const label = [fib.zone, fib.status].filter((v) => !!v).join(' · ');
    return label || 'Not Available';
  }

  protected stopText(): string {
    const stop = this.context()?.structuralStop;
    return typeof stop === 'number' && stop > 0 ? this.price(stop).toFixed(2) : 'Not Available';
  }

  protected close(): void {
    this.store.clearDetail();
  }
}
