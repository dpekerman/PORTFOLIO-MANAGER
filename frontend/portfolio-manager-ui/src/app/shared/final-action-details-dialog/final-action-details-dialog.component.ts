import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { FinalActionResult } from '../../core/models/portfolio.models';
import { describeReasonCode, stateLabel, technicalConflictNote } from './final-action-labels';

export interface FinalActionDetailsData {
  result: FinalActionResult;
}

/**
 * Compact, accessible explanation of one Final Action: the portfolio decision, the raw technical readings kept
 * separate, the reasons, and how fresh the evidence is. Read-only; it never computes or changes a decision.
 */
@Component({
  selector: 'app-final-action-details-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './final-action-details-dialog.component.html',
  styleUrl: './final-action-details-dialog.component.scss',
})
export class FinalActionDetailsDialogComponent {
  protected readonly r = inject<FinalActionDetailsData>(MAT_DIALOG_DATA).result;
  protected readonly state = stateLabel(this.r.evaluationState);
  protected readonly conflict = technicalConflictNote(this.r);
  protected readonly codes = (this.r.reasonCodes ?? []).map((c) => ({ code: c, text: describeReasonCode(c) }));
  protected readonly awaiting = this.r.finalAction === 'Awaiting Data';
}
