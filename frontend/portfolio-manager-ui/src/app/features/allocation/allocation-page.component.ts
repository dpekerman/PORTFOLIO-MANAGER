import { CurrencyPipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { SectorExpositionComponent } from './sector-exposition/sector-exposition.component';

@Component({
  selector: 'app-allocation-page',
  templateUrl: './allocation-page.component.html',
  styleUrl: './allocation-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    PercentPipe,
    MatCardModule,
    MatIconModule,
    MatTooltipModule,
    RouterLink,
    SectorExpositionComponent,
  ],
})
export class AllocationPageComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly demoMode = inject(DemoModeService);

  protected readonly isPositive = computed(() => this.portfolio.totalGainLoss() >= 0);
  protected readonly returnPct = computed(() => this.portfolio.displayTotalGainLossPct() / 100);
}
