import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { AddWatchlistDialogComponent } from './add-watchlist-dialog.component';

describe('AddWatchlistDialogComponent duplicate guard', () => {
  const close = vi.fn();
  const snack = { open: vi.fn() };

  function create(existing: string[]) {
    TestBed.configureTestingModule({
      providers: [
        { provide: MatDialogRef, useValue: { close } },
        { provide: MAT_DIALOG_DATA, useValue: { existingSymbols: existing } },
        { provide: MatSnackBar, useValue: snack },
        { provide: PortfolioApiService, useValue: { searchSymbols: vi.fn(() => of([])) } },
      ],
    });
    const fixture = TestBed.createComponent(AddWatchlistDialogComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    close.mockReset();
    snack.open.mockReset();
  });

  it('tags a selected ticker that already exists, warns, and blocks adding it again', () => {
    const fixture = create(['lmt.to', 'AAPL']);
    const cmp = fixture.componentInstance as unknown as {
      selectResult(r: { symbol: string; description: string; exchange: string }): void;
      confirm(): void;
    };

    cmp.selectResult({ symbol: 'LMT.TO', description: 'Lockheed', exchange: 'TSX' });
    fixture.detectChanges();

    expect(snack.open).toHaveBeenCalledWith(expect.stringContaining('LMT.TO already exists'), 'Close', expect.anything());
    expect(fixture.nativeElement.textContent).toContain('Already exists');
    const add = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Add',
    ) as HTMLButtonElement;
    expect(add.disabled).toBe(true);

    cmp.confirm();
    expect(close).not.toHaveBeenCalled();
  });

  it('allows a new ticker', () => {
    const fixture = create(['AAPL']);
    const cmp = fixture.componentInstance as unknown as {
      selectResult(r: { symbol: string; description: string; exchange: string }): void;
      confirm(): void;
    };

    cmp.selectResult({ symbol: 'MSFT', description: 'Microsoft', exchange: 'NASDAQ' });
    fixture.detectChanges();
    expect(snack.open).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain('Already exists');

    cmp.confirm();
    expect(close).toHaveBeenCalledWith({ symbol: 'MSFT', role: 'Strategic' });
  });
});
