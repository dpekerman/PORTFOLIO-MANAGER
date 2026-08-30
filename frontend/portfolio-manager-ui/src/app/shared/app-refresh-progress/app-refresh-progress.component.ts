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
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppRefreshService } from '../../core/services/app-refresh.service';

@Component({
  selector: 'app-refresh-progress',
  templateUrl: './app-refresh-progress.component.html',
  styleUrl: './app-refresh-progress.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
})
export class AppRefreshProgressComponent implements OnDestroy {
  protected readonly refresh = inject(AppRefreshService);
  private readonly snackBar = inject(MatSnackBar);

  /** Controls whether the completed overlay is still visible (auto-hides after 5s). */
  protected readonly showCompleted = signal(false);
  private hideTimer: ReturnType<typeof setTimeout> | null = null;
  private offlineWasShown = false;

  protected readonly isVisible = computed(
    () =>
      this.refresh.isRefreshing() ||
      this.showCompleted() ||
      this.refresh.isOffline() ||
      this.refresh.error(),
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
      if (wasRefreshing && !now && !this.refresh.error() && !this.refresh.isOffline()) {
        this.showCompleted.set(true);
        this.scheduleHide();
      }
      wasRefreshing = now;
    }, 200);

    // Watch for online state changes
    setInterval(() => {
      const isOnline = navigator.onLine;
      const isOffline = this.refresh.isOffline();

      if (isOnline && isOffline && !this.offlineWasShown) {
        this.offlineWasShown = true;
        this.showConnectionRestoredSnackbar();
      } else if (!isOnline && !isOffline) {
        this.offlineWasShown = false;
      }
    }, 500);
  }

  protected cancel(): void {
    this.refresh.cancelRefresh();
    this.showCompleted.set(false);
  }

  protected retry(): void {
    this.refresh.retry();
  }

  protected dismiss(): void {
    this.showCompleted.set(false);
  }

  protected getOfflineTitle(): string {
    const reason = this.refresh.offlineReason();
    switch (reason) {
      case 'network':
        return 'No internet connection';
      case 'server':
        return 'Server unavailable';
      case 'timeout':
        return 'Request timed out';
      default:
        return 'Offline';
    }
  }

  protected getOfflineMessage(): string {
    const reason = this.refresh.offlineReason();
    switch (reason) {
      case 'network':
        return 'Waiting for internet connection...';
      case 'server':
        return 'The server is not responding. Please try again later.';
      case 'timeout':
        return 'The request took too long to complete.';
      default:
        return 'An error occurred during refresh.';
    }
  }

  private scheduleHide(): void {
    if (this.hideTimer) clearTimeout(this.hideTimer);
    this.hideTimer = setTimeout(() => this.showCompleted.set(false), 5000);
  }

  private showConnectionRestoredSnackbar(): void {
    this.snackBar
      .open('Connection restored. Refresh data?', 'Refresh', {
        duration: 10000,
      })
      .onAction()
      .subscribe(() => {
        this.refresh.retry();
      });
  }

  ngOnDestroy(): void {
    if (this.hideTimer) clearTimeout(this.hideTimer);
  }
}
