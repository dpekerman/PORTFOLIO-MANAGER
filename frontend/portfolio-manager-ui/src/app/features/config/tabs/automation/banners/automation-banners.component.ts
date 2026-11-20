import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AutomationStore } from '../../../../../core/store/automation.store';
import { AllocConfirmDialogComponent } from '../../../alloc-confirm-dialog.component';

/** Live banners: run in progress (with Stop), recovery/backup results, active operation. */
@Component({
  selector: 'app-automation-banners',
  templateUrl: './automation-banners.component.html',
  styleUrl: './automation-banners.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
})
export class AutomationBannersComponent {
  protected readonly automationState = inject(AutomationStore);
  private readonly dialog = inject(MatDialog);

  protected cancelAutomationRun(): void {
    this.dialog
      .open(AllocConfirmDialogComponent, {
        data: {
          title: 'Stop automation run?',
          message:
            'This stops the in-progress run early. Steps already completed (e.g. market data refresh) are not undone; the run will be recorded as Cancelled.',
          confirmLabel: 'Stop Run',
          confirmIcon: 'stop_circle',
        },
        width: '380px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.automationState.cancelRun();
      });
  }
}
