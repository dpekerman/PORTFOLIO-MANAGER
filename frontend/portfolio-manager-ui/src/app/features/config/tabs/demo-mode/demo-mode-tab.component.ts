import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DemoModeStore, DemoStyle } from '../../../../core/store/demo-mode.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';

@Component({
  selector: 'app-demo-mode-tab',
  templateUrl: './demo-mode-tab.component.html',
  styleUrl: './demo-mode-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatSlideToggleModule,
    MatTooltipModule,
    SettingsSectionComponent,
  ],
})
export class DemoModeTabComponent implements OnInit {
  protected readonly demoMode = inject(DemoModeStore);
  private readonly snackBar = inject(MatSnackBar);

  // Pending values are applied only on Save.
  protected readonly pendingDemoEnabled = signal<boolean>(false);
  protected readonly pendingDemoStyle = signal<DemoStyle>('blur');
  protected readonly demoSettingsDirty = signal(false);

  ngOnInit(): void {
    this.initPendingDemo();
  }

  protected initPendingDemo(): void {
    this.pendingDemoEnabled.set(this.demoMode.isDemoMode());
    this.pendingDemoStyle.set(this.demoMode.demoStyle());
    this.demoSettingsDirty.set(false);
  }

  protected saveDemoSettings(): void {
    if (this.pendingDemoEnabled()) {
      this.demoMode.enable();
    } else {
      this.demoMode.disable();
    }
    this.demoMode.setStyle(this.pendingDemoStyle());
    this.demoSettingsDirty.set(false);
    this.snackBar.open('Demo mode settings saved', 'OK', { duration: 2500 });
  }
}
