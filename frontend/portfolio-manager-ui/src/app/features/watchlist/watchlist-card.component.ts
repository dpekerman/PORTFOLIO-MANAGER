import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { WatchlistSummary } from '../../core/models/portfolio.models';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';

@Component({
  selector: 'app-watchlist-card',
  templateUrl: './watchlist-card.component.html',
  styleUrl: './watchlist-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatTooltipModule,
    CurrencyPipe,
    DecimalPipe,
    NgClass,
  ],
})
export class WatchlistCardComponent {
  readonly summary = input.required<WatchlistSummary>();
  private readonly watchlist = inject(WatchlistStateService);

  protected readonly quote = computed(() => this.summary().quote);
  protected readonly isUp = computed(() => (this.quote()?.change ?? 0) >= 0);
  protected readonly hasData = computed(() => this.quote() !== null);

  remove(): void {
    this.watchlist.deleteItem(this.summary().item.id, this.summary().item.symbol);
  }
}
