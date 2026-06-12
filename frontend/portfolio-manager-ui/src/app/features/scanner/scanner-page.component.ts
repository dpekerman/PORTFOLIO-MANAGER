import { DatePipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { RsiScannerTableComponent } from '../rsi-scanner/rsi-scanner-table.component';

@Component({
  selector: 'app-scanner-page',
  templateUrl: './scanner-page.component.html',
  styleUrl: './scanner-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    NgClass,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    RsiScannerTableComponent,
  ],
})
export class ScannerPageComponent {
  protected readonly scanner = inject(ScannerStateService);

  refresh(): void {
    this.scanner.refresh(true);
  }
}
