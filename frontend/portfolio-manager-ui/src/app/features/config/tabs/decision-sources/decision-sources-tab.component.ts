import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DecisionSourcesStore } from '../../../../core/store/decision-sources.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { AllocConfirmDialogComponent } from '../../alloc-confirm-dialog.component';
import { ConfigSaveService } from '../../config-save.service';

@Component({
  selector: 'app-decision-sources-tab',
  templateUrl: './decision-sources-tab.component.html',
  styleUrl: './decision-sources-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule,
    SettingsSectionComponent,
  ],
})
export class DecisionSourcesTabComponent implements OnInit {
  private readonly store = inject(DecisionSourcesStore);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  protected readonly decisionSources = this.store.items;
  protected readonly decisionSourceDirty = this.store.dirty;
  protected readonly editingDecisionSource = signal<{ index: number | null; value: string } | null>(
    null,
  );
  protected readonly savingDecisionSources = signal(false);

  constructor() {
    inject(ConfigSaveService).register(
      { id: 'Decision sources', dirty: this.store.dirty, save: () => this.store.save() },
      inject(DestroyRef),
    );
  }

  ngOnInit(): void {
    this.store.load();
  }

  protected startAddDecisionSource(): void {
    this.editingDecisionSource.set({ index: null, value: '' });
  }

  protected startEditDecisionSource(index: number, value: string): void {
    this.editingDecisionSource.set({ index, value });
  }

  protected cancelEditDecisionSource(): void {
    this.editingDecisionSource.set(null);
  }

  protected updateEditingValue(value: string): void {
    this.editingDecisionSource.update((e) => (e ? { ...e, value } : e));
  }

  protected saveDecisionSourceRow(): void {
    const e = this.editingDecisionSource();
    if (!e || !e.value.trim()) return;
    this.store.upsert(e.value, e.index);
    this.editingDecisionSource.set(null);
  }

  protected deleteDecisionSource(index: number): void {
    this.dialog
      .open(AllocConfirmDialogComponent, {
        data: {
          title: 'Delete entry?',
          message: 'This entry will be removed when you save changes.',
        },
        width: '340px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.store.removeAt(index);
      });
  }

  protected saveDecisionSources(): void {
    this.savingDecisionSources.set(true);
    this.store.save().subscribe({
      next: () => {
        this.savingDecisionSources.set(false);
        this.snackBar.open('Decision Source list saved.', 'OK', { duration: 3000 });
      },
      error: () => {
        this.savingDecisionSources.set(false);
        this.snackBar.open('Failed to save Decision Sources.', 'Dismiss', { duration: 4000 });
      },
    });
  }
}
