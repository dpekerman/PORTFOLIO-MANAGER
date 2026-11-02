import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { ScannerStore } from '../../core/store/scanner.store';

@Component({
  selector: 'app-market-header',
  templateUrl: './market-header.component.html',
  styleUrl: './market-header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatTooltipModule, DecimalPipe],
})
export class MarketHeaderComponent {
  protected readonly scanner = inject(ScannerStore);
  protected readonly demoMode = inject(DemoModeStore);

  protected readonly signalBias = computed(() => {
    const o = this.scanner.oversold().length;
    const ob = this.scanner.overbought().length;
    if (o === 0 && ob === 0) return 'NEUTRAL';
    if (o > ob * 1.5) return 'OVERSOLD BIAS';
    if (ob > o * 1.5) return 'OVERBOUGHT BIAS';
    return 'MIXED';
  });

  protected readonly biasClass = computed(() => {
    const b = this.signalBias();
    if (b === 'OVERSOLD BIAS') return 'bias-oversold';
    if (b === 'OVERBOUGHT BIAS') return 'bias-overbought';
    return 'bias-neutral';
  });
}
