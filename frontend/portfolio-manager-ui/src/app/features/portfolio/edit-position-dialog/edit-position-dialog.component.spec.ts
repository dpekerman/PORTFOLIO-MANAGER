import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';
import { PortfolioItem } from '../../../core/models/portfolio.models';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { AccountTypesStore } from '../../../core/store/account-types.store';
import { ConfigStore } from '../../../core/store/config.store';
import {
  EditPositionDialogComponent,
  EditPositionDialogData,
  EditPositionDialogResult,
} from './edit-position-dialog.component';

const item = {
  id: 7,
  symbol: 'HUN.TO',
  companyName: 'Huntington',
  shares: 100,
  averageCostBasis: 10,
  sector: '',
  industry: '',
  sectorIsOverridden: false,
  transactionType: 'OPEN',
  accountType: 'A',
  openDate: '2026-09-01T00:00:00',
  closeDate: null,
  closingPrice: null,
  decisionSource: null,
  decisionSourceClosed: null,
  holdingRole: 'Swing',
} as unknown as PortfolioItem;

describe('EditPositionDialogComponent (PM-014 role save)', () => {
  let fixture: ComponentFixture<EditPositionDialogComponent>;
  let close: ReturnType<typeof vi.fn>;
  let submit: ReturnType<typeof vi.fn<(r: EditPositionDialogResult) => Promise<string | null>>>;

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const component = () => fixture.componentInstance as unknown as {
    form: { patchValue(v: unknown): void; markAsDirty(): void };
    save(): void;
  };

  async function create(outcome: Promise<string | null> | null): Promise<void> {
    close = vi.fn();
    submit = vi.fn(() => outcome ?? Promise.resolve(null));
    const data: EditPositionDialogData = { item, ...(outcome === undefined ? {} : { submit }) };
    await TestBed.configureTestingModule({
      imports: [EditPositionDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
        { provide: PortfolioApiService, useValue: { getSectorIndustryLists: () => of({ sectors: [], industries: [] }) } },
        { provide: ConfigStore, useValue: { config: () => ({ decisionSources: [] }) } },
        { provide: AccountTypesStore, useValue: { ready: signal(true), loading: signal(false), error: signal(null), names: signal(['A']), load: vi.fn() } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(EditPositionDialogComponent);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  function chooseSpeculative(): void {
    component().form.patchValue({ holdingRole: 'Speculative' });
    component().form.markAsDirty();
  }

  it('sends holdingRole and stays open showing the exact backend message when saving fails', async () => {
    await create(Promise.resolve('Other open lots have different roles.'));
    chooseSpeculative();

    component().save();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(submit).toHaveBeenCalledOnce();
    expect(submit.mock.calls[0][0].holdingRole).toBe('Speculative');
    expect(submit.mock.calls[0][0].shares).toBe(100);
    expect(close).not.toHaveBeenCalled();
    expect(text()).toContain('Other open lots have different roles.');
    expect(component().form).toBeTruthy();
    expect((fixture.componentInstance as unknown as { form: { value: { holdingRole: string } } }).form.value.holdingRole).toBe('Speculative');
  });

  it('closes only after a successful save', async () => {
    await create(Promise.resolve(null));
    chooseSpeculative();

    component().save();
    await fixture.whenStable();

    expect(close).toHaveBeenCalledOnce();
    expect(close).toHaveBeenCalledWith();
  });

  it('ignores a second save while one is in flight (no duplicate API calls)', async () => {
    let finish!: (v: string | null) => void;
    await create(new Promise<string | null>((r) => (finish = r)));
    chooseSpeculative();

    component().save();
    component().save();
    expect(submit).toHaveBeenCalledOnce();

    finish(null);
    await fixture.whenStable();
    expect(close).toHaveBeenCalledOnce();
  });

  it('states that a role change does not touch broker orders', async () => {
    await create(Promise.resolve(null));
    fixture.detectChanges();
    expect(text()).toContain('does not change quantity, cost, transactions or broker orders');
  });

  describe('PM-020 close position entry', () => {
    async function createClose(sources: string[]) {
      await TestBed.configureTestingModule({
        imports: [EditPositionDialogComponent],
        providers: [
          { provide: MAT_DIALOG_DATA, useValue: { item, closePosition: { defaultDecisionSourceClosed: 'Stop Loss Trigerred' } } },
          { provide: MatDialogRef, useValue: { close: vi.fn() } },
          { provide: PortfolioApiService, useValue: { getSectorIndustryLists: () => of({ sectors: [], industries: [] }) } },
          { provide: ConfigStore, useValue: { config: () => ({ decisionSources: sources }) } },
          { provide: AccountTypesStore, useValue: { ready: signal(true), loading: signal(false), error: signal(null), names: signal(['A']), load: vi.fn() } },
        ],
      }).compileComponents();
      fixture = TestBed.createComponent(EditPositionDialogComponent);
      fixture.detectChanges();
      return fixture.componentInstance as unknown as {
        form: { value: Record<string, unknown>; controls: { closingPrice: { setValue(v: number | null): void } } };
      };
    }

    it('prefills a CLOSE with today, an empty price and the exact default source when it exists', async () => {
      const c = await createClose(['Manual', 'Stop Loss Trigerred', 'App Signal']);
      expect(c.form.value['transactionType']).toBe('CLOSE');
      expect(c.form.value['closeDate']).toBeInstanceOf(Date);
      expect(c.form.value['closingPrice']).toBeNull();
      expect(c.form.value['decisionSourceClosed']).toBe('Stop Loss Trigerred');
      expect(c.form.value['shares']).toBe(100);
    });

    it('leaves the source blank when the exact value is not configured', async () => {
      const c = await createClose(['Manual', 'Stop Loss Triggered']);
      expect(c.form.value['decisionSourceClosed']).toBeNull();
    });

    it('requires the actual execution price before saving', async () => {
      const c = await createClose(['Manual']);
      const comp = fixture.componentInstance as unknown as { save(): void };
      comp.save();
      expect(fixture.componentInstance).toBeTruthy();
      c.form.controls.closingPrice.setValue(11.5);
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Enter the actual execution price');
    });

    it('lists decision sources alphabetically without changing the values', async () => {
      await createClose(['manual', 'App Signal', 'catalyst']);
      const sorted = (fixture.componentInstance as unknown as { decisionSources: string[] }).decisionSources;
      expect(sorted).toEqual(['App Signal', 'catalyst', 'manual']);
    });
  });

  describe('PM-018 close date', () => {
    const form = () =>
      (fixture.componentInstance as unknown as {
        form: {
          controls: { closeDate: { value: Date | null; invalid: boolean; setValue(v: Date | null): void } };
          patchValue(v: unknown): void;
        };
      }).form;

    it('defaults an empty Close Date when switching OPEN to CLOSE and blocks saving without one', async () => {
      await create(Promise.resolve(null));
      form().patchValue({ transactionType: 'CLOSE' });
      expect(form().controls.closeDate.value).toBeInstanceOf(Date);

      form().controls.closeDate.setValue(null);
      component().save();
      expect(submit).not.toHaveBeenCalled();
    });

    it('never overwrites a Close Date chosen by the user', async () => {
      await create(Promise.resolve(null));
      const chosen = new Date(2026, 9, 5, 12);
      form().controls.closeDate.setValue(chosen);
      form().patchValue({ transactionType: 'CLOSE' });
      form().patchValue({ shares: 50 });
      expect(form().controls.closeDate.value).toBe(chosen);
    });

    it('rejects a Close Date before the Open Date', async () => {
      await create(Promise.resolve(null));
      form().patchValue({ transactionType: 'CLOSE' });
      form().controls.closeDate.setValue(new Date(2026, 7, 1, 12));
      expect(form().controls.closeDate.invalid).toBe(true);
      component().save();
      expect(submit).not.toHaveBeenCalled();
    });
  });
});
