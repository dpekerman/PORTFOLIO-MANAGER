import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppRefreshService } from '../../core/services/app-refresh.service';

@Component({
  selector: 'app-refresh-button',
  templateUrl: './app-refresh-button.component.html',
  styleUrl: './app-refresh-button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
})
export class AppRefreshButtonComponent {
  protected readonly appRefresh = inject(AppRefreshService);

  readonly tooltip = input('Refresh all data (bypasses 4-min cache)');
  readonly label = input('');
  /** Extra busy state from the host page (e.g. a page-specific reload in flight). */
  readonly busy = input(false);

  /** Emitted after the global refresh starts so the host page can run its own page-specific reload. */
  readonly refreshed = output<void>();

  protected click(): void {
    this.appRefresh.refreshAll('user-click');
    this.refreshed.emit();
  }
}
