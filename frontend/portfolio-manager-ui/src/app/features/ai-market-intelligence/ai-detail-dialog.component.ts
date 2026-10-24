import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { FeedbackRating, GlobalEvent, NewsItem, StockAudit } from '../../core/models/premarket.models';
import { PreMarketStore } from '../../core/store/premarket.store';

export interface AiDetailDialogData {
  runId: number | null;
  stock?: StockAudit;
  event?: GlobalEvent;
}

/** Read-only detail for one security or one global event: evidence links, AI notes, warnings, feedback. */
@Component({
  selector: 'app-ai-detail-dialog',
  templateUrl: './ai-detail-dialog.component.html',
  styleUrl: './ai-detail-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, NgTemplateOutlet, MatButtonModule, MatDialogModule],
})
export class AiDetailDialogComponent {
  protected readonly data = inject<AiDetailDialogData>(MAT_DIALOG_DATA);
  private readonly store = inject(PreMarketStore);

  protected readonly ratings: { value: FeedbackRating; label: string }[] = [
    { value: 'USEFUL', label: 'Useful' },
    { value: 'NOT_USEFUL', label: 'Not useful' },
    { value: 'ACTION_TAKEN', label: 'Action taken' },
    { value: 'NO_ACTION_TAKEN', label: 'No action' },
  ];

  protected feedback(rating: FeedbackRating): void {
    if (this.data.runId !== null) this.store.sendFeedback(this.data.runId, rating, this.data.stock?.symbol ?? null);
  }

  protected label(n: NewsItem): string {
    return n.isMock ? `${n.headline} (mock)` : n.headline;
  }
}
