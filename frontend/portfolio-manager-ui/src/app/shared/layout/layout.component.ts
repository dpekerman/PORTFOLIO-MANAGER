import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { AuthApiService } from '../../core/services/auth-api.service';
import { AuthStateService } from '../../core/services/auth-state.service';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { ThemeService } from '../../core/services/theme.service';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { MarketHeaderComponent } from '../market-header/market-header.component';

@Component({
  selector: 'app-layout',
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatTooltipModule,
    MatDividerModule,
    MarketHeaderComponent,
  ],
})
export class LayoutComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly scanner = inject(ScannerStateService);
  protected readonly theme = inject(ThemeService);
  protected readonly watchlist = inject(WatchlistStateService);
  protected readonly demoMode = inject(DemoModeService);
  protected readonly authState = inject(AuthStateService);
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);

  protected readonly sidenav = viewChild.required<MatSidenav>('sidenav');

  protected readonly isLoading = computed(
    () => this.portfolio.loading() || this.scanner.loading() || this.watchlist.loading(),
  );

  /** Show RSI market header only on the RSI Scanner page. */
  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );
  protected readonly showMarketHeader = computed(() => this.currentUrl().startsWith('/scanner'));

  protected readonly navLinks = [
    { path: '/portfolio', label: 'Portfolio', icon: 'account_balance_wallet' },
    { path: '/watchlist', label: 'Watchlist', icon: 'visibility' },
    { path: '/scanner', label: 'RSI Scanner', icon: 'radar' },
    { path: '/eod-signals', label: 'EOD Signals', icon: 'timeline' },
    { path: '/transactions', label: 'Transactions', icon: 'receipt_long' },
    { path: '/allocation', label: 'Allocation', icon: 'donut_large' },
    { path: '/value-screener', label: 'Value Screener', icon: 'analytics' },
    { path: '/config', label: 'Configuration', icon: 'settings' },
  ] as const;

  refreshAll(): void {
    this.portfolio.refresh();
    this.scanner.refresh(true);
    this.watchlist.refresh();
  }

  logout(): void {
    this.authApi.logout().subscribe();
    this.authState.clearAuth();
    this.router.navigate(['/login']);
  }

  closeSidenav(): void {
    this.sidenav().close();
  }
}
