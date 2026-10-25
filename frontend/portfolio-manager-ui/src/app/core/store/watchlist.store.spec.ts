import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { WatchlistSummary } from '../models/portfolio.models';
import { AuthStore } from './auth.store';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { WatchlistStore } from './watchlist.store';

const summary = (id: number, symbol: string): WatchlistSummary =>
  ({
    item: { id, symbol, isFavorite: false, role: 'Strategic', notes: '' },
    quote: null,
    priceStructure: null,
    technicalFacts: null,
  }) as unknown as WatchlistSummary;

describe('WatchlistStore', () => {
  const authenticated = signal(false);
  const api = {
    getWatchlistSnapshot: vi.fn(),
    getWatchlist: vi.fn(),
    addWatchlistItem: vi.fn(),
    deleteWatchlistItem: vi.fn(),
    updateWatchlistFavorite: vi.fn(),
    getPeriodChanges: vi.fn(),
  };
  const snackBar = { open: vi.fn() };

  beforeEach(() => {
    authenticated.set(false);
    Object.values(api).forEach((fn) => fn.mockReset());
    snackBar.open.mockReset();
    api.getWatchlistSnapshot.mockReturnValue(of([summary(1, 'AAPL')]));
    api.getWatchlist.mockReturnValue(of([summary(2, 'MSFT')]));
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  function store(): InstanceType<typeof WatchlistStore> {
    const s = TestBed.inject(WatchlistStore);
    TestBed.tick();
    return s;
  }

  it('does not load until the user is authenticated', () => {
    const s = store();
    expect(api.getWatchlistSnapshot).not.toHaveBeenCalled();

    authenticated.set(true);
    TestBed.tick();

    expect(api.getWatchlistSnapshot).toHaveBeenCalledTimes(1);
    expect(s.items().map((x) => x.item.symbol)).toEqual(['AAPL']);
    expect(s.fromSnapshot()).toBe(true);
    expect(s.loading()).toBe(false);
    expect(s.count()).toBe(1);
  });

  it('falls back to a live refresh when there is no snapshot', () => {
    api.getWatchlistSnapshot.mockReturnValue(of(null));
    const s = store();
    authenticated.set(true);
    TestBed.tick();

    expect(api.getWatchlist).toHaveBeenCalledTimes(1);
    expect(s.items().map((x) => x.item.symbol)).toEqual(['MSFT']);
    expect(s.fromSnapshot()).toBe(false);
  });

  it('falls back to a live refresh when the snapshot request fails', () => {
    api.getWatchlistSnapshot.mockReturnValue(throwError(() => new Error('boom')));
    const s = store();
    authenticated.set(true);
    TestBed.tick();

    expect(api.getWatchlist).toHaveBeenCalledTimes(1);
    expect(s.items().length).toBe(1);
  });

  it('reports a refresh failure and stops loading', () => {
    api.getWatchlist.mockReturnValue(throwError(() => new Error('down')));
    const s = store();

    s.refresh();

    expect(s.loading()).toBe(false);
    expect(s.error()).toBe('Failed to load watchlist');
  });

  it('applies an update only after the server confirms it', () => {
    const s = store();
    s.setFromRefresh([summary(1, 'AAPL')]);

    api.updateWatchlistFavorite.mockReturnValueOnce(throwError(() => new Error('no')));
    s.updateFavorite(1, true);
    expect(s.items()[0].item.isFavorite).toBe(false);
    expect(snackBar.open).toHaveBeenCalledWith('Failed to update favourite', 'Close', {
      duration: 4000,
    });

    api.updateWatchlistFavorite.mockReturnValueOnce(of(undefined));
    s.updateFavorite(1, true);
    expect(s.items()[0].item.isFavorite).toBe(true);
  });

  it('appends an added item and surfaces the server error title on failure', async () => {
    const s = store();
    api.addWatchlistItem.mockReturnValueOnce(
      of({ id: 9, symbol: 'NVDA', notes: '', addedAt: '2026-01-01', role: 'Strategic' }),
    );
    await s.addItem('nvda');
    expect(s.items().map((x) => x.item.symbol)).toEqual(['NVDA']);

    api.addWatchlistItem.mockReturnValueOnce(
      throwError(() => ({ error: { title: 'Symbol already exists' } })),
    );
    await expect(s.addItem('nvda')).rejects.toBeDefined();
    expect(snackBar.open).toHaveBeenLastCalledWith('Symbol already exists', 'Close', {
      duration: 4000,
    });
  });

  it('removes an item once the delete succeeds', () => {
    const s = store();
    s.setFromRefresh([summary(1, 'AAPL'), summary(2, 'MSFT')]);
    api.deleteWatchlistItem.mockReturnValueOnce(of(undefined));

    s.deleteItem(1, 'AAPL');

    expect(s.items().map((x) => x.item.symbol)).toEqual(['MSFT']);
  });

  it('merges 5D / 1M period changes by symbol and keeps prior values when a reload fails', () => {
    const s = store();
    api.getPeriodChanges.mockReturnValueOnce(
      of([{ symbol: 'aapl', change5DPct: 1.5, change1MPct: null }]),
    );
    s.loadPeriodChanges(['AAPL']);
    expect(s.periodChanges()['AAPL']).toEqual({ symbol: 'aapl', change5DPct: 1.5, change1MPct: null });

    api.getPeriodChanges.mockReturnValueOnce(throwError(() => new Error('yahoo down')));
    s.loadPeriodChanges(['AAPL', 'MSFT']);
    expect(s.periodChanges()['AAPL'].change5DPct).toBe(1.5);

    s.loadPeriodChanges([]);
    expect(api.getPeriodChanges).toHaveBeenCalledTimes(2);
  });
});
