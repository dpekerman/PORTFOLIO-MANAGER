import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthStore } from '../../core/store/auth.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { AppRefreshButtonComponent } from '../../shared/app-refresh-button/app-refresh-button.component';
import { SettingsSectionComponent } from '../../shared/ui/settings-section/settings-section.component';
import { CONFIG_TABS, ConfigTabId } from './config-tabs';
import { ConfigSaveService } from './config-save.service';
import { AccountTypesComponent } from './tabs/account-types/account-types.component';
import { AlertsTabComponent } from './tabs/alerts/alerts-tab.component';
import { AllocationTabComponent } from './tabs/allocation/allocation-tab.component';
import { AutomationTabComponent } from './tabs/automation/automation-tab.component';
import { DecisionSourcesTabComponent } from './tabs/decision-sources/decision-sources-tab.component';
import { DemoModeTabComponent } from './tabs/demo-mode/demo-mode-tab.component';
import { EodWindowTabComponent } from './tabs/eod-window/eod-window-tab.component';
import { HistoryTabComponent } from './tabs/history/history-tab.component';
import { ScannerTabComponent } from './tabs/scanner/scanner-tab.component';
import { SectorsTabComponent } from './tabs/sectors/sectors-tab.component';
import { SignalsAuditTabComponent } from './tabs/signals-audit/signals-audit-tab.component';
import { UsersTabComponent } from './tabs/users/users-tab.component';
import { ValueScreenerTabComponent } from './tabs/value-screener/value-screener-tab.component';

/**
 * Config shell: tab strip + global Save/Reset. Each tab is its own component, created the first
 * time it is opened and kept alive afterwards so unsaved edits survive tab switches. Tabs register
 * with {@link ConfigSaveService}, which the global Save All / Reset All act on.
 */
@Component({
  selector: 'app-config-page',
  templateUrl: './config-page.component.html',
  styleUrl: './config-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Hints/errors take real space (instead of an absolutely-positioned overlay) so they can wrap to
  // several lines on phones without colliding with the next field.
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { subscriptSizing: 'dynamic' } }],
  imports: [
    AppRefreshButtonComponent,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    SettingsSectionComponent,
    AccountTypesComponent,
    AlertsTabComponent,
    AllocationTabComponent,
    AutomationTabComponent,
    DecisionSourcesTabComponent,
    DemoModeTabComponent,
    EodWindowTabComponent,
    HistoryTabComponent,
    ScannerTabComponent,
    SectorsTabComponent,
    SignalsAuditTabComponent,
    UsersTabComponent,
    ValueScreenerTabComponent,
  ],
})
export class ConfigPageComponent {
  protected readonly demoMode = inject(DemoModeStore);
  protected readonly save = inject(ConfigSaveService);
  private readonly authState = inject(AuthStore);
  protected readonly isAdmin = this.authState.isAdmin;

  protected readonly activeSection = signal<ConfigTabId>('scanner');
  private readonly visited = signal<ReadonlySet<ConfigTabId>>(new Set<ConfigTabId>(['scanner']));

  protected readonly tabs = computed(() =>
    CONFIG_TABS.filter((t) => !t.adminOnly || this.authState.isAdmin()),
  );

  protected setSection(id: ConfigTabId): void {
    this.activeSection.set(id);
    this.visited.update((set) => (set.has(id) ? set : new Set([...set, id])));
  }

  protected isVisited(id: ConfigTabId): boolean {
    return this.visited().has(id);
  }
}
