import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { SwingTradeRow } from '../../core/models/swing-trading.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { SwingDetailChipComponent } from './swing-detail-chip.component';
import { riskStatusLabel, severityClass } from './swing-trading-logic';
import { riskTone, riskView } from './swing-presenters';

/**
 * Severity plus the most material risk statuses. Remaining statuses sit behind a real button (click, tap, Enter or
 * Space), never a hover-only tooltip, and every status stays reachable.
 */
@Component({
  selector: 'app-swing-risk-cell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SwingDetailChipComponent],
  templateUrl: './swing-risk-cell.component.html',
  styleUrl: './swing-risk-cell.component.scss',
})
export class SwingRiskCellComponent {
  readonly row = input.required<SwingTradeRow>();
  protected readonly demo = inject(DemoModeStore);
  protected readonly expanded = signal(false);
  protected readonly view = computed(() => riskView(this.row()));
  protected readonly shown = computed(() => (this.expanded() ? [...this.view().visible, ...this.view().hidden] : this.view().visible));
  protected readonly severityClass = severityClass;
  protected readonly label = riskStatusLabel;
  protected readonly tone = riskTone;

  protected toggle(): void {
    this.expanded.update((v) => !v);
  }
}
