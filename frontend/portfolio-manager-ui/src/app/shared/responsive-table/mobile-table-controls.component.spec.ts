import { Component, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSort, MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { BreakpointStore } from '../../core/store/breakpoint.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { UserPreferencesStore } from '../../core/store/user-preferences.store';
import { MobileTableControlsComponent } from './mobile-table-controls.component';

const isHandset = signal(true);

@Component({
  template: `
    @if (sort(); as s) {
      <app-mobile-table-controls [sort]="s" gridId="transactions-stocks" label="Stock" />
    }
    <table mat-table [dataSource]="[]" matSort (matSortChange)="lastSort.set($event)">
      <ng-container matColumnDef="tx_symbol">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>TICKER</th>
        <td mat-cell *matCellDef="let r"></td>
      </ng-container>
      <ng-container matColumnDef="tx_open_date">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>OPEN DATE</th>
        <td mat-cell *matCellDef="let r"></td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
  `,
  imports: [MatTableModule, MatSortModule, MobileTableControlsComponent],
})
class HostComponent {
  readonly columns = ['tx_symbol', 'tx_open_date'];
  readonly sort = viewChild(MatSort);
  readonly lastSort = signal<Sort | null>(null);
}

describe('MobileTableControlsComponent', () => {
  beforeEach(() => {
    localStorage.clear();
    isHandset.set(true);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: UserPreferencesStore,
          useValue: { get: () => null, set: () => {}, remove: () => {} },
        },
        {
          provide: BreakpointStore,
          useValue: { isHandset, isTablet: signal(false), isDesktop: signal(false) },
        },
      ],
    });
  });

  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    // Second pass + tick: the viewChild'd MatSort resolves, then the afterEveryRender snapshot of
    // its registered sortables runs.
    fixture.detectChanges();
    TestBed.tick();
    fixture.detectChanges();
    return fixture;
  }

  it('renders nothing when the viewport is not a handset', () => {
    isHandset.set(false);
    const fixture = setup();
    expect(fixture.nativeElement.querySelector('.mtc-bar')).toBeNull();
  });

  it('renders the sort select, direction toggle and show-all toggle on a handset', () => {
    const el = setup().nativeElement as HTMLElement;
    expect(el.querySelector('.mtc-bar')).not.toBeNull();
    expect(el.querySelector('mat-select')).not.toBeNull();
    expect(el.querySelector('.mtc-dir-btn')).not.toBeNull();
    expect(el.querySelector('mat-slide-toggle')).not.toBeNull();
  });

  it('offers only the sortable columns currently on screen', () => {
    const fixture = setup();
    const controls = fixture.debugElement.children
      .map((d) => d.componentInstance)
      .find((c) => c instanceof MobileTableControlsComponent) as MobileTableControlsComponent;

    const options = (controls as unknown as { sortOptions: () => { key: string }[] }).sortOptions();
    expect(options.map((o) => o.key)).toEqual(['tx_symbol', 'tx_open_date']);
  });

  it('emits sortChange with the chosen column and direction', () => {
    const fixture = setup();
    const host = fixture.componentInstance;
    const controls = fixture.debugElement.children
      .map((d) => d.componentInstance)
      .find((c) => c instanceof MobileTableControlsComponent) as unknown as {
      onSortKeyChange: (key: string) => void;
      toggleDirection: () => void;
      direction: () => string;
    };

    controls.onSortKeyChange('tx_symbol');
    expect(host.lastSort()).toEqual({ active: 'tx_symbol', direction: 'asc' });
    expect(host.sort()!.active).toBe('tx_symbol');

    controls.toggleDirection();
    expect(host.lastSort()).toEqual({ active: 'tx_symbol', direction: 'desc' });
    expect(controls.direction()).toBe('desc');
  });

  it('drives showAllColumnsOnHandset without persisting it', () => {
    const fixture = setup();
    const grid = TestBed.inject(GridColumnStore);
    const controls = fixture.debugElement.children
      .map((d) => d.componentInstance)
      .find((c) => c instanceof MobileTableControlsComponent) as unknown as {
      onShowAllChange: (v: boolean) => void;
    };

    expect(grid.showAllColumnsOnHandset()).toBe(false);
    controls.onShowAllChange(true);
    expect(grid.showAllColumnsOnHandset()).toBe(true);
    expect(localStorage.getItem('pm_grid_columns_v1')).toBeNull();
  });
});
