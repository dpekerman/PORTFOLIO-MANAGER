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
});
