import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'ui-section-header',
  imports: [],
  templateUrl: './section-header.html',
  styleUrl: './section-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SectionHeader {
  readonly title = input.required<string>();
  readonly description = input<string>();
}
