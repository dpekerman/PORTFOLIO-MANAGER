import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { PortfolioItem } from '../models/portfolio.models';
import { PortfolioStore } from '../store/portfolio.store';
import { swingRow } from '../../features/swing-trading/swing-row.testing';
import { SwingClosePositionService } from './swing-close-position.service';
import { EditPositionDialogData, EditPositionDialogResult } from '../../features/portfolio/edit-position-dialog/edit-position-dialog.component';

const lot = (id: number, shares: number, over: Partial<PortfolioItem> = {}): PortfolioItem =>
  ({
    id,
    symbol: 'NOVO.TO',
    companyName: 'Novo',
    shares,
    averageCostBasis: 17,
    accountType: 'TFSA_D_TD',
    holdingRole: 'Swing',
    transactionType: 'OPEN',
    openDate: '2026-10-15T00:00:00',
    ...over,
  }) as PortfolioItem;

describe('SwingClosePositionService (PM-020)', () => {
  let items: PortfolioItem[];
  let dialogOpen: ReturnType<typeof vi.fn>;
  let updateItem: ReturnType<typeof vi.fn>;
  let snackOpen: ReturnType<typeof vi.fn>;
  let pickerResult: number | undefined;

  const result = (over: Partial<EditPositionDialogResult> = {}): EditPositionDialogResult => ({
    companyName: 'Novo',
    shares: 600,
    averageCostBasis: 17,
    sector: '',
    industry: '',
    overrideSector: false,
    transactionType: 'CLOSE',
    accountType: 'TFSA_D_TD',
    openDate: '2026-10-15',
    closeDate: '2026-11-01',
    closingPrice: 11.5,
    decisionSource: null,
    decisionSourceClosed: null,
    holdingRole: 'Swing',
    ...over,
  });

  function service(): SwingClosePositionService {
    dialogOpen = vi.fn((component: { name: string }) =>
      component.name.includes('Picker') ? { afterClosed: () => of(pickerResult) } : { afterClosed: () => of(undefined) },
    );
    updateItem = vi.fn();
    snackOpen = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        { provide: MatDialog, useValue: { open: dialogOpen } },
        { provide: MatSnackBar, useValue: { open: snackOpen } },
        { provide: PortfolioStore, useValue: { summaries: () => items.map((item) => ({ item, quote: null })), updateItem } },
      ],
    });
    return TestBed.inject(SwingClosePositionService);
  }

  const editData = (): EditPositionDialogData =>
    dialogOpen.mock.calls.find((c) => (c[0] as { name: string }).name.includes('EditPosition'))![1].data;

  beforeEach(() => {
    items = [lot(639, 600)];
    pickerResult = undefined;
  });

  it('opens the existing Edit Position dialog directly for a single lot, with the exact default source requested', async () => {
    const svc = service();
    await svc.open(swingRow({ lotIds: [639] }));

    const data = editData();
    expect(data.item.id).toBe(639);
    expect(data.closePosition?.defaultDecisionSourceClosed).toBe('Stop Loss Trigerred');
    expect(updateItem).not.toHaveBeenCalled(); // opening records nothing
  });

  it('opens nothing and tells the user when no lot is eligible', async () => {
    items = [lot(639, 600, { transactionType: 'CLOSE' })];
    const svc = service();
    expect(svc.hasEligibleLot(swingRow())).toBe(false);
    await svc.open(swingRow());
    expect(dialogOpen).not.toHaveBeenCalled();
    expect(snackOpen).toHaveBeenCalled();
  });

  it('asks which lot for a multi-lot position and uses exactly that PortfolioItem', async () => {
    items = [lot(10, 100), lot(12, 200), lot(13, 50, { holdingRole: 'Core' })];
    pickerResult = 12;
    const svc = service();
    await svc.open(swingRow({ lotIds: [10, 12, 13] }));

    const picker = dialogOpen.mock.calls[0][1].data;
    expect(picker.lots.map((l: { id: number }) => l.id)).toEqual([10, 12]);
    expect(editData().item.id).toBe(12);
  });

  it('cancelling the lot picker makes no change', async () => {
    items = [lot(10, 100), lot(12, 200)];
    pickerResult = undefined;
    const svc = service();
    await svc.open(swingRow({ lotIds: [10, 12] }));
    expect(dialogOpen).toHaveBeenCalledTimes(1);
    expect(updateItem).not.toHaveBeenCalled();
  });

  it('submits through the existing updateItem path as a CLOSE of that lot', async () => {
    const svc = service();
    await svc.open(swingRow());
    updateItem.mockImplementation((_id, _req, cb) => cb.success());

    const outcome = await editData().submit!(result({ shares: 200 }));

    expect(outcome).toBeNull();
    expect(updateItem).toHaveBeenCalledOnce();
    const [id, request] = updateItem.mock.calls[0];
    expect(id).toBe(639);
    expect(request).toMatchObject({ transactionType: 'CLOSE', shares: 200, closingPrice: 11.5, closeDate: '2026-11-01' });
  });

  it('returns the backend reason on a failed save and allows a retry', async () => {
    const svc = service();
    await svc.open(swingRow());
    updateItem.mockImplementationOnce((_id, _req, cb) => cb.failure('Rejected'));
    expect(await editData().submit!(result())).toBe('Rejected');

    updateItem.mockImplementationOnce((_id, _req, cb) => cb.success());
    expect(await editData().submit!(result())).toBeNull();
    expect(updateItem).toHaveBeenCalledTimes(2);
  });

  it('blocks a duplicate submission while one is in flight', async () => {
    const svc = service();
    await svc.open(swingRow());
    updateItem.mockImplementation(() => undefined); // never completes

    void editData().submit!(result());
    const second = await editData().submit!(result());

    expect(updateItem).toHaveBeenCalledOnce();
    expect(second).toContain('already being saved');
  });

  it('refuses to submit when the lot changed after the dialog opened', async () => {
    const svc = service();
    await svc.open(swingRow());
    items = [lot(639, 400)];

    const outcome = await editData().submit!(result());

    expect(outcome).toContain('changed');
    expect(updateItem).not.toHaveBeenCalled();
  });
});
