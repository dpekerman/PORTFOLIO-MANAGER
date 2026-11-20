import { ChangeDetectionStrategy, Component, ViewEncapsulation, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Card chrome for a settings group: icon, title, subtitle and an optional header badge.
 * Project the body (`.section-body`, forms, footers) as content. Loads the shared settings-UI
 * library (`styles/_config-ui.scss`, scoped under `.cfg-tab`) globally, but only once a settings
 * section is rendered, so it stays out of the initial bundle.
 */
@Component({
  selector: 'app-settings-section',
  templateUrl: './settings-section.component.html',
  styleUrl: './settings-section.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  imports: [MatIconModule],
})
export class SettingsSectionComponent {
  readonly icon = input.required<string>();
  readonly title = input.required<string>();
  readonly subtitle = input<string>('');
  /** Highlights the card border and header icon (e.g. while a time window is active). */
  readonly accent = input<boolean>(false);
}
