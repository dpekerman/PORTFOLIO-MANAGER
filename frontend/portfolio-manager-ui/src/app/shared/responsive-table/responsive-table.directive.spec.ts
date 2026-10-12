import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule } from '@angular/material/sort';
import { BreakpointStore } from '../../core/store/breakpoint.store';
import { ResponsiveTableDirective } from './responsive-table.directive';

const isHandset = signal(false);

@Component({
  template: `
    <table mat-table appResponsiveTable [dataSource]="rows()" matSort>
      <ng-container matColumnDef="symbol">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>TICKER</th>
        <td mat-cell *matCellDef="let r">{{ r.symbol }}</td>
      </ng-container>
      <ng-container matColumnDef="gain_loss">
        <th mat-header-cell *matHeaderCellDef>Gain "net" \\ total</th>
        <td mat-cell *matCellDef="let r">{{ r.gain }}</td>
      </ng-container>
      <ng-container matColumnDef="tx_actions">
        <th mat-header-cell *matHeaderCellDef>ACTIONS</th>
        <td mat-cell *matCellDef="let r"></td>
      </ng-container>
      <ng-container matColumnDef="colConfig">
        <th mat-header-cell *matHeaderCellDef>CONFIG</th>
        <td mat-cell *matCellDef="let r"></td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns()"></tr>
      <tr mat-row *matRowDef="let row; columns: columns()"></tr>
    </table>
  `,
  imports: [MatTableModule, MatSortModule, ResponsiveTableDirective],
})
class HostComponent {
  readonly rows = signal([{ symbol: 'AAPL', gain: 12 }]);
  readonly columns = signal(['symbol', 'gain_loss', 'tx_actions', 'colConfig']);
}

describe('ResponsiveTableDirective', () => {
  beforeEach(() => {
    isHandset.set(false);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: BreakpointStore,
          useValue: { isHandset, isTablet: signal(false), isDesktop: signal(true) },
        },
      ],
    });
  });

  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('table') as HTMLTableElement;
    return { fixture, table };
  }

  /** CSS text of the stylesheet this table injected, or '' when there is none. */
  function injectedCss(table: HTMLTableElement): string {
    const id = table.getAttribute('data-responsive-table');
    const el = document.head.querySelector<HTMLStyleElement>(`style[data-responsive-table="${id}"]`);
    return el?.textContent ?? '';
  }

  it('leaves the table untouched on desktop', () => {
    const { table } = setup();
    expect(table.getAttribute('data-responsive')).toBeNull();
    expect(injectedCss(table)).toBe('');
  });

  it('flags card mode on a handset', () => {
    const { fixture, table } = setup();
    isHandset.set(true);
    fixture.detectChanges();
    expect(table.getAttribute('data-responsive')).toBe('cards');
  });

  it('generates one ::before label rule per labelled column, scoped to this table', () => {
    const { fixture, table } = setup();
    isHandset.set(true);
    fixture.detectChanges();

    const css = injectedCss(table);
    const id = table.getAttribute('data-responsive-table');
    expect(css).toContain(
      `table[data-responsive-table="${id}"][data-responsive='cards'] td.mat-column-symbol::before{content:"TICKER"}`,
    );
    expect(css.split('\n')).toHaveLength(2);
  });

  it('escapes quotes and backslashes in header text', () => {
    const { fixture, table } = setup();
    isHandset.set(true);
    fixture.detectChanges();

    expect(injectedCss(table)).toContain('content:"Gain \\"net\\" \\\\ total"');
  });

  it('never labels the actions or config columns', () => {
    const { fixture, table } = setup();
    isHandset.set(true);
    fixture.detectChanges();

    const css = injectedCss(table);
    expect(css).not.toContain('mat-column-tx_actions');
    expect(css).not.toContain('mat-column-colConfig');
  });

  it('re-labels when the displayed columns change', async () => {
    const { fixture, table } = setup();
    isHandset.set(true);
    fixture.detectChanges();

    fixture.componentInstance.columns.set(['gain_loss']);
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve));

    const css = injectedCss(table);
    expect(css).not.toContain('mat-column-symbol::before');
    expect(css).toContain('mat-column-gain_loss::before');
  });

  it('removes the stylesheet when the viewport grows again', () => {
    const { fixture, table } = setup();
    isHandset.set(true);
    fixture.detectChanges();
    expect(injectedCss(table)).not.toBe('');

    isHandset.set(false);
    fixture.detectChanges();
    expect(table.getAttribute('data-responsive')).toBeNull();
    expect(injectedCss(table)).toBe('');
  });

  it('cleans up the stylesheet on destroy', () => {
    const { fixture, table } = setup();
    isHandset.set(true);
    fixture.detectChanges();
    const id = table.getAttribute('data-responsive-table');

    fixture.destroy();

    expect(document.head.querySelector(`style[data-responsive-table="${id}"]`)).toBeNull();
  });
});
