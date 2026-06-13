import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';

@Component({
  selector: 'app-market-header',
  templateUrl: './market-header.component.html',
  styleUrl: './market-header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatTooltipModule, DecimalPipe, CurrencyPipe, NgClass],
})
export class MarketHeaderComponent {
  protected readonly scanner = inject(ScannerStateService);
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly demoMode = inject(DemoModeService);

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

  protected readonly isPortfolioPositive = computed(() => this.portfolio.totalGainLoss() >= 0);

  protected readonly displayPortfolioValue = computed(() =>
    this.demoMode.maskValue(this.portfolio.totalValue()),
  );

  protected readonly displayGainLossPct = computed(() =>
    this.demoMode.maskPercent(this.portfolio.totalGainLossPct()),
  );
}
