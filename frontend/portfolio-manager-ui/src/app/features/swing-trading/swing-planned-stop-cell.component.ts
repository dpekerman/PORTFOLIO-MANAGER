import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { SwingTradeRow } from '../../core/models/swing-trading.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { plannedStopView } from './swing-presenters';
import { SwingDetailChipComponent } from './swing-detail-chip.component';

/** Planned Stop price plus the backend's breach verdict. Highlights a breach in red; never implies a broker execution. */
@Component({
  selector: 'app-swing-planned-stop-cell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, SwingDetailChipComponent],
  templateUrl: './swing-planned-stop-cell.component.html',
  styleUrl: './swing-planned-stop-cell.component.scss',
})
export class SwingPlannedStopCellComponent {
  readonly row = input.required<SwingTradeRow>();
  private readonly demo = inject(DemoModeStore);

  protected readonly masked = computed(() => {
    const v = this.row().plannedStop;
    return v === null ? null : this.demo.maskValue(v);
  });

  protected readonly view = computed(() =>
    plannedStopView(this.row(), (n) => (n === null ? '—' : this.demo.maskValue(n).toFixed(2))),
  );
}
