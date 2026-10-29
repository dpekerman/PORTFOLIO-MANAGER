import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RiskStatus } from '../../core/models/swing-trading.models';
import { SwingTradingStore } from '../../core/store/swing-trading.store';
import { PROTECTION_OPTIONS, RISK_STATUS_LABELS } from './swing-trading-logic';

/** Filters shared by Open Trades and Risk Monitor, so both tabs always show the same dataset. */
@Component({
  selector: 'app-swing-filter-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, MatSlideToggleModule],
  templateUrl: './swing-filter-bar.component.html',
  styleUrl: './swing-filter-bar.component.scss',
})
export class SwingFilterBarComponent {
  protected readonly store = inject(SwingTradingStore);
  protected readonly protectionOptions = PROTECTION_OPTIONS;
  protected readonly riskOptions = (Object.keys(RISK_STATUS_LABELS) as RiskStatus[]).map((key) => ({
    key,
    label: RISK_STATUS_LABELS[key],
  }));

  protected onSearch(event: Event): void {
    this.store.setFilters({ search: (event.target as HTMLInputElement).value });
  }
}
