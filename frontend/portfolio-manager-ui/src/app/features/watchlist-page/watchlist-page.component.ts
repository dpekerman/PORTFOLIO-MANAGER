import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { WatchlistSummary } from '../../core/models/portfolio.models';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { WatchlistCardSkeletonComponent } from '../../shared/skeleton/watchlist-card-skeleton.component';
import { AddWatchlistDialogComponent } from './add-watchlist-dialog.component';
import { WatchlistCardComponent } from './watchlist-card.component';

type ViewMode = 'card' | 'grid';
type SortColumn = 'symbol' | 'company' | 'price' | 'change' | 'changePct' | 'sector';
type SortDir = 'asc' | 'desc';

@Component({
  selector: 'app-watchlist-page',
  templateUrl: './watchlist-page.component.html',
  styleUrl: './watchlist-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    CurrencyPipe,
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    WatchlistCardComponent,
    WatchlistCardSkeletonComponent,
  ],
})
export class WatchlistPageComponent {
  protected readonly watchlist = inject(WatchlistStateService);
  private readonly dialog = inject(MatDialog);

  protected readonly viewMode = signal<ViewMode>('grid');
  protected readonly filterText = signal('');
  protected readonly sortCol = signal<SortColumn>('symbol');
  protected readonly sortDir = signal<SortDir>('asc');

  protected readonly displayedColumns: string[] = [
    'symbol',
    'company',
    'price',
    'change',
    'changePct',
    'sector',
    'actions',
  ];

  protected readonly filteredSorted = computed<WatchlistSummary[]>(() => {
    const filter = this.filterText().trim().toLowerCase();
    let items = this.watchlist.items();

    if (filter) {
      items = items.filter(
        (w) =>
          w.item.symbol.toLowerCase().includes(filter) ||
          (w.quote?.companyName ?? '').toLowerCase().includes(filter) ||
          (w.quote?.sector ?? '').toLowerCase().includes(filter),
      );
    }

    const col = this.sortCol();
    const dir = this.sortDir() === 'asc' ? 1 : -1;

    return [...items].sort((a, b) => {
      let av: string | number;
      let bv: string | number;
      switch (col) {
        case 'symbol':
          av = a.item.symbol;
          bv = b.item.symbol;
          break;
        case 'company':
          av = a.quote?.companyName ?? '';
          bv = b.quote?.companyName ?? '';
          break;
        case 'price':
          av = a.quote?.currentPrice ?? 0;
          bv = b.quote?.currentPrice ?? 0;
          break;
        case 'change':
          av = a.quote?.change ?? 0;
          bv = b.quote?.change ?? 0;
          break;
        case 'changePct':
          av = a.quote?.changePercent ?? 0;
          bv = b.quote?.changePercent ?? 0;
          break;
        case 'sector':
          av = a.quote?.sector ?? '';
          bv = b.quote?.sector ?? '';
          break;
        default:
          av = a.item.symbol;
          bv = b.item.symbol;
      }
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  setSort(col: SortColumn): void {
    if (this.sortCol() === col) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortCol.set(col);
      this.sortDir.set('asc');
    }
  }

  onMatSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.sortCol.set(sort.active as SortColumn);
    this.sortDir.set(sort.direction as SortDir);
  }

  openAddDialog(): void {
    this.dialog
      .open(AddWatchlistDialogComponent, { width: '420px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((symbol: string | null) => {
        if (symbol) this.watchlist.addItem(symbol);
      });
  }

  refresh(): void {
    this.watchlist.refresh();
  }

  remove(w: WatchlistSummary): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Symbol',
          message: `Remove ${w.item.symbol} from your watchlist?`,
          confirmLabel: 'Remove',
          danger: true,
        },
        width: '360px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.watchlist.deleteItem(w.item.id, w.item.symbol);
      });
  }
}
