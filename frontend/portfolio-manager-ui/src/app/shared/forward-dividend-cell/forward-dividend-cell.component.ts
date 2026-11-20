import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ForwardDividendMetric, forwardDividendOf } from '../../core/forward-dividend.util';
import { StockQuote } from '../../core/models/portfolio.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';

/** One Fwd Div/Share or Fwd Yield % cell, shared so Portfolio and Watchlist present the data identically. */
@Component({
  selector: 'app-forward-dividend-cell',
  templateUrl: './forward-dividend-cell.component.html',
  styleUrl: './forward-dividend-cell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, DecimalPipe, MatTooltipModule],
})
export class ForwardDividendCellComponent {
  protected readonly demoMode = inject(DemoModeStore);

  readonly quote = input<StockQuote | null | undefined>(null);
  readonly currency = input<string>('USD');
  readonly metric = input.required<ForwardDividendMetric>();

  protected readonly view = computed(() => forwardDividendOf(this.quote()));

  protected readonly value = computed(() => {
    const v = this.view();
    const raw = this.metric() === 'perShare' ? v.perShare : v.yieldPct;
    if (raw === null) return null;
    return this.metric() === 'perShare' ? this.demoMode.maskValue(raw) : this.demoMode.maskPercent(raw);
  });
}
