import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { WatchlistCardSkeletonComponent } from '../../shared/skeleton/watchlist-card-skeleton.component';
import { AddWatchlistDialogComponent } from './add-watchlist-dialog.component';
import { WatchlistCardComponent } from './watchlist-card.component';

@Component({
  selector: 'app-watchlist-page',
  templateUrl: './watchlist-page.component.html',
  styleUrl: './watchlist-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    WatchlistCardComponent,
    WatchlistCardSkeletonComponent,
  ],
})
export class WatchlistPageComponent {
  protected readonly watchlist = inject(WatchlistStateService);
  private readonly dialog = inject(MatDialog);

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
}
