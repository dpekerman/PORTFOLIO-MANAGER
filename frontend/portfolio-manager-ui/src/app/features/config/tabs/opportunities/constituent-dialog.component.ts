import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { IndexConstituentRequest } from '../../../../core/models/opportunities.models';
import { constituentFormErrors } from './opportunity-reference-logic';

export interface ConstituentDialogData {
  /** Pre-filled so a sourced list can be entered row by row. */
  effectiveFrom: string;
  source: string;
  sourceAsOfDate: string;
}

/** Add one TSX 60 constituent. Closes with the request, or undefined when cancelled. */
@Component({
  selector: 'app-constituent-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './constituent-dialog.component.html',
  styleUrl: './reference-dialog.scss',
})
export class ConstituentDialogComponent {
  private readonly data = inject<ConstituentDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ConstituentDialogComponent, IndexConstituentRequest | undefined>);

  protected readonly model = signal({
    symbol: '',
    companyName: '',
    effectiveFrom: this.data.effectiveFrom,
    source: this.data.source,
    sourceAsOfDate: this.data.sourceAsOfDate,
  });
  protected readonly f = form(this.model);
  protected readonly errors = computed(() => constituentFormErrors(this.model()));

  protected submit(): void {
    if (this.errors().length) return;
    const m = this.model();
    this.ref.close({
      symbol: m.symbol.trim(),
      companyName: m.companyName.trim() || null,
      indexCode: null,
      effectiveFrom: m.effectiveFrom,
      source: m.source.trim(),
      sourceAsOfDate: m.sourceAsOfDate,
    });
  }
}
