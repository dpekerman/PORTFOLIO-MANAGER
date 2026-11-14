import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatTooltipModule } from '@angular/material/tooltip';

/**
 * Filterable list with add/remove. Presentational: the parent owns the items and reacts to
 * `added` / `removed`. Styled by the shared settings-UI library (`.list-panel`, under `.cfg-tab`).
 */
@Component({
  selector: 'app-list-editor',
  templateUrl: './list-editor.component.html',
  styleUrl: './list-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatTooltipModule,
  ],
})
export class ListEditorComponent {
  readonly items = input.required<string[]>();
  /** Plural heading, e.g. "Sectors". */
  readonly heading = input.required<string>();
  /** Singular noun used in labels, e.g. "sector". */
  readonly noun = input.required<string>();
  readonly placeholder = input<string>('');

  readonly added = output<string>();
  readonly removed = output<string>();

  protected readonly filter = signal('');
  protected readonly newValue = signal('');
  protected readonly filtered = computed(() => {
    const f = this.filter().toLowerCase();
    return f ? this.items().filter((s) => s.toLowerCase().includes(f)) : this.items();
  });

  protected add(): void {
    const value = this.newValue();
    if (value.trim()) this.added.emit(value);
    this.newValue.set('');
  }
}
