import { ChangeDetectionStrategy, Component, ViewEncapsulation, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Standard page heading: icon, uppercase title (+ optional badge), subtitle, extra meta line and
 * an actions area. Wraps on phones so long action rows never overflow.
 *
 * Slots: `[pageTitleExtra]` (badge after the title), `[pageSub]` (appended to the subtitle),
 * `[pageMeta]` (extra line under the subtitle) and `[pageActions]` (right-hand buttons).
 * Class names (`page-header`, `page-actions`, …) are kept so the global responsive rules still apply.
 */
@Component({
  selector: 'app-page-header',
  templateUrl: './page-header.component.html',
  styleUrl: './page-header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  imports: [MatIconModule],
})
export class PageHeaderComponent {
  readonly icon = input.required<string>();
  readonly title = input.required<string>();
  readonly subtitle = input<string>('');
  /** Icon colour; defaults to the theme primary. */
  readonly accent = input<string>('');
}
