import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AllocationRiskStore } from '../../../../core/store/allocation-risk.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { ConfigSaveService } from '../../config-save.service';
import { TargetPanelComponent, TargetRowEdit } from './target-panel/target-panel.component';

@Component({
  selector: 'app-allocation-tab',
  templateUrl: './allocation-tab.component.html',
  styleUrl: './allocation-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    SettingsSectionComponent,
    TargetPanelComponent,
  ],
})
export class AllocationTabComponent implements OnInit {
  private readonly store = inject(AllocationRiskStore);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly riskRows = this.store.riskRows;
  protected readonly sectorRows = this.store.sectorRows;
  protected readonly limitRows = this.store.limitRows;
  protected readonly allocationDirty = this.store.dirty;
  protected readonly savingAllocation = signal(false);

  constructor() {
    inject(ConfigSaveService).register(
      { id: 'Allocation & risk', dirty: this.store.dirty, save: () => this.store.save() },
      inject(DestroyRef),
    );
  }

  ngOnInit(): void {
    this.store.load();
  }

  protected upsertRisk(e: TargetRowEdit): void {
    this.store.upsert('risk', e.id, e.label, e.targetPct);
  }

  protected upsertSector(e: TargetRowEdit): void {
    this.store.upsert('sector', e.id, e.label, e.targetPct);
  }

  protected upsertLimit(e: TargetRowEdit): void {
    this.store.upsert('limit', e.id, e.label, e.targetPct);
  }

  protected deleteRisk(id: number): void {
    this.store.remove('risk', id);
  }

  protected deleteSector(id: number): void {
    this.store.remove('sector', id);
  }

  protected deleteLimit(id: number): void {
    this.store.remove('limit', id);
  }

  protected saveAllocationRisk(): void {
    this.savingAllocation.set(true);
    this.store.save().subscribe({
      next: () => {
        this.savingAllocation.set(false);
        this.snackBar.open('Allocation & Risk settings saved.', 'OK', { duration: 3000 });
      },
      error: () => {
        this.savingAllocation.set(false);
        this.snackBar.open('Failed to save allocation settings.', 'Dismiss', { duration: 4000 });
      },
    });
  }
}
