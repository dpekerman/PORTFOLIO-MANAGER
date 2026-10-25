import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { SignalAuditRow, SignalMyAction } from '../../core/models/portfolio.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { SignalsAuditStore } from '../../core/store/signals-audit.store';

export interface SignalActionDialogData {
  row: SignalAuditRow;
}

interface ActionChoice {
  value: SignalMyAction;
  label: string;
  hint: string;
}

const MAX_NOTE = 1000;

/**
 * Records the investor's decision on one signal. It only links to transactions that already exist
 * and never creates or edits one; transaction proximity is a suggestion the user confirms.
 */
@Component({
  selector: 'app-signal-action-dialog',
  templateUrl: './signal-action-dialog.component.html',
  styleUrl: './signal-action-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatRadioModule,
  ],
})
export class SignalActionDialogComponent {
  protected readonly data = inject<SignalActionDialogData>(MAT_DIALOG_DATA);
  protected readonly store = inject(SignalsAuditStore);
  protected readonly demoMode = inject(DemoModeStore);
  private readonly dialogRef = inject(MatDialogRef<SignalActionDialogComponent>);

  protected readonly row = this.data.row;
  protected readonly maxNote = MAX_NOTE;

  protected readonly choices: ActionChoice[] = [
    { value: 'Actioned', label: 'Actioned', hint: 'I followed the recommendation.' },
    { value: 'Partial', label: 'Partial', hint: 'I partially followed the recommendation.' },
    { value: 'Not Actioned', label: 'Not Actioned', hint: 'I decided not to follow the recommendation.' },
    { value: 'Pending', label: 'Pending', hint: 'No confirmed decision yet.' },
  ];

  protected readonly myAction = signal<SignalMyAction>(this.row.myAction);
  protected readonly note = signal('');
  protected readonly linkedTransactionId = signal<number | null>(this.row.linkedTransactionId);
  protected readonly saving = signal(false);
  private readonly initialised = signal(false);

  protected readonly followed = computed(() => this.myAction() === 'Actioned' || this.myAction() === 'Partial');
  protected readonly noteTooLong = computed(() => this.note().length > MAX_NOTE);
  protected readonly isEdit = this.row.myAction !== 'Pending';

  constructor() {
    // Existing note/link are only on the detail payload; load them once when editing.
    this.store.openDetail(this.row.id);
    effect(() => {
      const detail = this.store.detail();
      if (!detail || detail.row.id !== this.row.id || untracked(this.initialised)) return;
      this.note.set(detail.note ?? '');
      this.initialised.set(true);
    });

    // Search for matching transactions whenever the user claims they followed the signal.
    let searched = false;
    effect(() => {
      if (this.followed() && !searched) {
        searched = true;
        untracked(() => this.store.loadCandidates(this.row.id));
      }
    });
  }

  protected price(): number {
    return this.demoMode.maskValue(this.row.signalPrice);
  }

  protected candidatePrice(price: number): number {
    return this.demoMode.maskValue(price);
  }

  protected shares(shares: number): number {
    return this.demoMode.isDemoMode() ? this.demoMode.maskValue(shares) : shares;
  }

  protected async save(): Promise<void> {
    if (this.saving() || this.noteTooLong()) return;
    this.saving.set(true);
    const ok = await this.store.saveAction(
      this.row.id,
      this.myAction(),
      this.note().trim() || null,
      this.followed() ? this.linkedTransactionId() : null,
    );
    this.saving.set(false);
    if (ok) this.dialogRef.close(true);
  }

  protected cancel(): void {
    this.dialogRef.close(false);
  }
}
