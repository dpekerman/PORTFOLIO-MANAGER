import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HistoryBackfillStore } from '../../../../core/store/history-backfill.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';

@Component({
  selector: 'app-history-tab',
  templateUrl: './history-tab.component.html',
  styleUrl: './history-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    SettingsSectionComponent,
  ],
})
export class HistoryTabComponent {
  private readonly store = inject(HistoryBackfillStore);

  protected readonly historyMissingDays = this.store.missingDays;
  protected readonly historyScanning = this.store.scanning;
  protected readonly historyReconstructing = this.store.reconstructing;
  protected readonly historyReconstructResult = this.store.result;
  protected readonly historyScanned = this.store.scanned;

  protected scanMissingDays(): void {
    this.store.scan();
  }

  protected reconstructMissingDays(): void {
    this.store.reconstruct();
  }
}
