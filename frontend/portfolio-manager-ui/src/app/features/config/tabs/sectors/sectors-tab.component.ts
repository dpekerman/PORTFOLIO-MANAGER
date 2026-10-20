import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ConfigStore } from '../../../../core/store/config.store';
import { SectorIndustryStore } from '../../../../core/store/sector-industry.store';
import { ListEditorComponent } from '../../../../shared/ui/list-editor/list-editor.component';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { ConfigSaveService } from '../../config-save.service';

@Component({
  selector: 'app-sectors-tab',
  templateUrl: './sectors-tab.component.html',
  styleUrl: './sectors-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    ListEditorComponent,
    SettingsSectionComponent,
  ],
})
export class SectorsTabComponent implements OnInit {
  private readonly store = inject(SectorIndustryStore);
  private readonly configService = inject(ConfigStore);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly sectors = this.store.sectors;
  protected readonly industries = this.store.industries;
  protected readonly savingLists = signal(false);

  constructor() {
    inject(ConfigSaveService).register(
      { id: 'Sectors & industries', dirty: this.store.dirty, save: () => this.store.save() },
      inject(DestroyRef),
    );

    effect(() => {
      const error = this.store.error();
      if (error) this.snackBar.open(error, 'Dismiss', { duration: 4000 });
    });
  }

  ngOnInit(): void {
    this.store.load();
  }

  protected addSector(value: string): void {
    this.store.addSector(value);
  }

  protected removeSector(value: string): void {
    this.store.removeSector(value);
  }

  protected addIndustry(value: string): void {
    this.store.addIndustry(value);
  }

  protected removeIndustry(value: string): void {
    this.store.removeIndustry(value);
  }

  protected saveSectorIndustryLists(): void {
    this.savingLists.set(true);
    this.store.save(this.configService.config().decisionSources).subscribe({
      next: () => {
        this.savingLists.set(false);
        this.snackBar.open('Sector & Industry lists saved.', 'OK', { duration: 3000 });
      },
      error: () => {
        this.savingLists.set(false);
        this.snackBar.open('Failed to save lists.', 'Dismiss', { duration: 4000 });
      },
    });
  }
}
