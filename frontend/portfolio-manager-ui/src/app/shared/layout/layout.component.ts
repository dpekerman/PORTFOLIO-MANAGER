import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { AppRefreshStore } from '../../core/store/app-refresh.store';
import { AuthApiService } from '../../core/services/auth-api.service';
import { AuthStore } from '../../core/store/auth.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { ScannerStore } from '../../core/store/scanner.store';
import { ScoreAlertStore } from '../../core/store/score-alert.store';
import { ThemeStore } from '../../core/store/theme.store';
import { AppRefreshProgressComponent } from '../app-refresh-progress/app-refresh-progress.component';
import { MarketHeaderComponent } from '../market-header/market-header.component';

interface NavLink {
  path: string;
  label: string;
  icon: string;
}

interface NavGroup {
  id: string;
  label: string;
  icon: string;
  links: readonly NavLink[];
}

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
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatMenuModule,
    MatTooltipModule,
    MatDividerModule,
    MarketHeaderComponent,
    AppRefreshProgressComponent,
  ],
})
export class LayoutComponent {
  protected readonly scanner = inject(ScannerStore);
  protected readonly theme = inject(ThemeStore);
  protected readonly demoMode = inject(DemoModeStore);
  protected readonly authState = inject(AuthStore);
  // Instantiate to activate auto-refresh timer and the RSI analysis / Action Center sync it starts
  protected readonly appRefresh = inject(AppRefreshStore);
  // Instantiate to activate the score e-mail alert that runs after each refresh
  private readonly scoreAlert = inject(ScoreAlertStore);
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);

  protected readonly sidenav = viewChild.required<MatSidenav>('sidenav');

  protected readonly offlineBadgeTooltip = computed(() => {
    switch (this.appRefresh.offlineReason()) {
      case 'network':
        return 'No internet connection — will retry automatically';
      case 'timeout':
        return 'Request timed out — will retry automatically';
      case 'server':
        return 'Server unavailable — click refresh to retry manually';
      default:
        return 'Offline';
    }
  });

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

  /**
   * Grouped navigation: the four most-used destinations stay one click away; the rest sit in two
   * labelled menus so the top bar stays short. The same model feeds the phone drawer.
   */
  protected readonly primaryLinks: readonly NavLink[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/portfolio', label: 'Portfolio', icon: 'account_balance_wallet' },
    { path: '/watchlist', label: 'Watchlist', icon: 'visibility' },
    { path: '/transactions', label: 'Transactions', icon: 'receipt_long' },
    { path: '/swing-trading', label: 'Swing Trading', icon: 'swap_vert' },
  ];

  protected readonly navGroups: readonly NavGroup[] = [
    {
      id: 'markets',
      label: 'Markets',
      icon: 'query_stats',
      links: [
        { path: '/scanner', label: 'RSI Scanner', icon: 'radar' },
        { path: '/eod-signals', label: 'EOD Signals', icon: 'timeline' },
        { path: '/value-screener', label: 'Value Screener', icon: 'analytics' },
      ],
    },
    {
      id: 'insights',
      label: 'Insights',
      icon: 'insights',
      links: [
        { path: '/allocation', label: 'Allocation', icon: 'donut_large' },
        { path: '/portfolio-value-history', label: 'Value History', icon: 'history' },
        { path: '/signal-tracker', label: 'Signal Tracker', icon: 'track_changes' },
        { path: '/audit', label: 'Audit', icon: 'fact_check' },
      ],
    },
  ];

  protected readonly settingsLink: NavLink = {
    path: '/config',
    label: 'Configuration',
    icon: 'settings',
  };

  /** A group button is highlighted while any of its pages is open. */
  protected isGroupActive(group: NavGroup): boolean {
    const url = this.currentUrl();
    return group.links.some((l) => url === l.path || url.startsWith(`${l.path}/`) || url.startsWith(`${l.path}?`));
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
