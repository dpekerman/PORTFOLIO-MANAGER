import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppRefreshService } from '../../core/services/app-refresh.service';

@Component({
  selector: 'app-refresh-progress',
  templateUrl: './app-refresh-progress.component.html',
  styleUrl: './app-refresh-progress.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatTooltipModule],
})
export class AppRefreshProgressComponent implements OnDestroy {
  protected readonly refresh = inject(AppRefreshService);

  /** Controls whether the completed overlay is still visible (auto-hides after 5s). */
  protected readonly showCompleted = signal(false);
  private hideTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly isVisible = computed(
    () => this.refresh.isRefreshing() || this.showCompleted(),
  );

  protected readonly elapsedLabel = computed(() => {
    const secs = this.refresh.secondsSinceRefresh();
    if (secs === null) return null;
    if (secs < 60) return 'just now';
    const min = Math.floor(secs / 60);
    return `${min} min ago`;
  });

  constructor() {
    // Watch for refresh completion to start the auto-hide timer
    let wasRefreshing = false;
    setInterval(() => {
      const now = this.refresh.isRefreshing();
      if (wasRefreshing && !now && !this.refresh.error()) {
        this.showCompleted.set(true);
        this.scheduleHide();
      }
      wasRefreshing = now;
    }, 200);
  }

  private scheduleHide(): void {
    if (this.hideTimer) clearTimeout(this.hideTimer);
    this.hideTimer = setTimeout(() => this.showCompleted.set(false), 5000);
  }

  ngOnDestroy(): void {
    if (this.hideTimer) clearTimeout(this.hideTimer);
  }
}
