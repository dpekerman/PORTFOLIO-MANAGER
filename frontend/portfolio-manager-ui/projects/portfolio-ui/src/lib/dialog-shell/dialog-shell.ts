import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'ui-dialog-shell',
  imports: [],
  templateUrl: './dialog-shell.html',
  styleUrl: './dialog-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DialogShell {
  readonly title = input.required<string>();
  readonly description = input<string>();
}
