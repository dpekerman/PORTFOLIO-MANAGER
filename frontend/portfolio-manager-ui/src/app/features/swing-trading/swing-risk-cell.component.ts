import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { SwingTradeRow } from '../../core/models/swing-trading.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { DetailChipTone, SwingDetailChipComponent } from './swing-detail-chip.component';
import { riskStatusLabel } from './swing-trading-logic';
import { compactRisk, riskTone } from './swing-presenters';

/**
 * Compact risk classification (CRITICAL/HIGH/MEDIUM/LOW/UNKNOWN) from the backend severity. Every risk flag is in the
 * chip's tooltip and click/tap detail; a breached stop always stays visible as its own chip.
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
  protected readonly compact = computed(() => compactRisk(this.row()));
  protected readonly critical = computed(() => this.row().riskStatuses.filter((s) => riskTone(s) === 'bad'));
  protected readonly severityTone = computed<DetailChipTone>(() => {
    switch (this.compact().label) {
      case 'CRITICAL': return 'bad';
      case 'HIGH':
      case 'MEDIUM':
      case 'UNKNOWN': return 'warn';
      default: return 'dim';
    }
  });
  protected readonly label = riskStatusLabel;
  protected readonly tone = riskTone;
}
