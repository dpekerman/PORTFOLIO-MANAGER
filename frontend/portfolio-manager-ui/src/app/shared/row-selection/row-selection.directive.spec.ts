import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatTableModule } from '@angular/material/table';
import { RowSelectionDirective, SelectableRowDirective } from './row-selection.directive';

@Component({
  imports: [MatTableModule, RowSelectionDirective, SelectableRowDirective],
  template: `
    <table mat-table appRowSelection [dataSource]="rows">
      <ng-container matColumnDef="name">
        <td mat-cell *matCellDef="let r"><button type="button">{{ r.name }}</button></td>
      </ng-container>
      <tr mat-row *matRowDef="let row; columns: ['name']" [appSelectableRow]="row.id"></tr>
    </table>
  `,
})
class HostComponent {
  rows = [
    { id: 1, name: 'A' },
    { id: 2, name: 'B' },
  ];
}

describe('row selection', () => {
  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const rows = (): HTMLElement[] =>
      Array.from(fixture.nativeElement.querySelectorAll('tr.mat-mdc-row'));
    return { fixture, rows };
  }

  it('selects on click and moves selection to another row', async () => {
    const { fixture, rows } = setup();
    rows()[0].click();
    await fixture.whenStable();
    expect(rows()[0].classList).toContain('row-selected');
    rows()[1].querySelector('button')!.click();
    await fixture.whenStable();
    expect(rows()[0].classList).not.toContain('row-selected');
    expect(rows()[1].classList).toContain('row-selected');
  });

  it('keeps selection after a data refresh with new row objects', async () => {
    const { fixture, rows } = setup();
    rows()[1].click();
    fixture.componentInstance.rows = [
      { id: 1, name: 'A' },
      { id: 2, name: 'B2' },
    ];
    fixture.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(rows()[1].classList).toContain('row-selected');
  });

  it('deselects when the selected row background is clicked again', async () => {
    const { fixture, rows } = setup();
    rows()[0].click();
    rows()[0].click();
    await fixture.whenStable();
    expect(rows()[0].classList).not.toContain('row-selected');
  });
});
