import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { NotificationApiService } from '../services/notification-api.service';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { AllocationRiskStore } from './allocation-risk.store';
import { DecisionSourcesStore } from './decision-sources.store';
import { NotificationRecipientsStore } from './notification-recipients.store';
import { SectorIndustryStore } from './sector-industry.store';

describe('config stores', () => {
  const api = {
    getSectorIndustryLists: vi.fn(() => of({ sectors: ['Tech'], industries: ['Banks'] })),
    saveSectorIndustryLists: vi.fn((l: { sectors: string[]; industries: string[] }) => of(l)),
    getDecisionSources: vi.fn(() => of({ items: [] as string[] })),
    saveDecisionSourcesList: vi.fn((items: string[]) => of({ items })),
    getAllocationRiskConfig: vi.fn(() =>
      of({
        riskTargets: [{ id: 1, role: 'Core', targetPct: 60, displayOrder: 0 }],
        sectorTargets: [],
        positionLimits: [],
      }),
    ),
    deleteRiskTarget: vi.fn(() => of(void 0)),
    upsertRiskTarget: vi.fn(() => of(void 0)),
    upsertSectorTarget: vi.fn(() => of(void 0)),
    upsertPositionLimit: vi.fn(() => of(void 0)),
    deleteSectorTarget: vi.fn(() => of(void 0)),
    deletePositionLimit: vi.fn(() => of(void 0)),
  };
  const notifications = {
    getRecipients: vi.fn(() => of({ emails: ['a@x.com'] })),
    updateRecipients: vi.fn((emails: string[]) => of({ emails })),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: NotificationApiService, useValue: notifications },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
      ],
    });
  });

  it('sector store: dedupes, sorts, tracks dirty and clears it on save', () => {
    const store = TestBed.inject(SectorIndustryStore);
    store.load();
    expect(store.sectors()).toEqual(['Tech']);
    expect(store.dirty()).toBe(false);

    store.addSector('Energy');
    store.addSector('Energy');
    store.addSector('  ');
    expect(store.sectors()).toEqual(['Energy', 'Tech']);
    expect(store.dirty()).toBe(true);

    store.save().subscribe();
    expect(store.dirty()).toBe(false);
  });

  it('recipients store: normalises, rejects invalid and duplicates', () => {
    const store = TestBed.inject(NotificationRecipientsStore);
    store.load();
    store.add('  A@X.com ');
    store.add('nope');
    store.add('b@x.com');
    expect(store.emails()).toEqual(['a@x.com', 'b@x.com']);
    store.removeLast();
    expect(store.emails()).toEqual(['a@x.com']);
    expect(store.dirty()).toBe(true);
  });

  it('decision sources store: falls back to defaults when the server list is empty', () => {
    const store = TestBed.inject(DecisionSourcesStore);
    store.load();
    expect(store.items().length).toBeGreaterThan(0);
    store.upsert('Earnings Play', null);
    store.upsert('Earnings Play', null);
    expect(store.items().filter((i) => i === 'Earnings Play')).toHaveLength(1);
    expect(store.dirty()).toBe(true);
  });

  it('allocation store: stages edits and deletes, flushes them on save', () => {
    const store = TestBed.inject(AllocationRiskStore);
    store.load();
    store.upsert('risk', null, 'Swing', 10);
    store.remove('risk', 1);
    expect(store.riskRows().map((r) => r.label)).toEqual(['Swing']);
    expect(store.dirty()).toBe(true);

    store.save().subscribe();

    expect(api.deleteRiskTarget).toHaveBeenCalledWith(1);
    expect(api.upsertRiskTarget).toHaveBeenCalledWith(null, 'Swing', 10);
    expect(store.dirty()).toBe(false);
  });
});
