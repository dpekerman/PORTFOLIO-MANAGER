import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ScreenRefreshService } from '../../core/services/screen-refresh.service';

@Component({
  selector: 'app-screen-refresh-progress',
  templateUrl: './screen-refresh-progress.component.html',
  styleUrl: './screen-refresh-progress.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatTooltipModule],
})
export class ScreenRefreshProgressComponent {
  readonly refreshService = input.required<ScreenRefreshService>();

  /** Emitted so the parent can actually stop the underlying operation, not just hide this popup. */
  readonly cancelled = output<void>();

  protected cancel(): void {
    this.refreshService().cancel();
    this.cancelled.emit();
  }
}
