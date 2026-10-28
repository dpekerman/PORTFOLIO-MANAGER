import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { BrokerStop, ManageRiskDialogData, RiskPosition } from '../../core/models/swing-trading.models';
import { SwingTradingStore } from '../../core/store/swing-trading.store';
import { ManageRiskDialogComponent } from './manage-risk-dialog.component';

const stop = (over: Partial<BrokerStop> = {}): BrokerStop => ({
  id: 1, orderIdentity: 'o1', orderType: 'StopMarket', triggerPrice: 16.2, limitPrice: null, currency: 'CAD',
  userStatus: 'UserConfirmedEstablished', brokerVerificationStatus: 'Unverified', coveredQuantity: null, placedDate: null,
  brokerOrderRef: null, lastUserVerifiedDate: null, confirmationSource: 'UserSuppliedStopSheet', notes: null,
  episodeOpenDate: '2026-10-15T00:00:00', isActive: true, belongsToCurrentEpisode: true, brokerCancellationUnverified: false,
  replacedByRecordId: null, updatedAtUtc: '2026-10-28T10:00:00Z', rowVersion: 'v1', ...over,
});

const position = (over: Partial<RiskPosition> = {}): RiskPosition => ({
  accountType: 'TFSA_D_TD', symbol: 'NOVO.TO', hasOpenPosition: true, totalQuantity: 600, swingQuantity: 600, mixedRole: false,
  episodeResolution: 'Derived', episodeNote: null, episodeOpenDate: '2026-10-15T00:00:00', currency: 'CAD', currencyBasis: 'AssumedFromSuffix',
  plan: null, brokerStops: [stop()], history: [], ...over,
});

const data: ManageRiskDialogData = {
  account: 'TFSA_D_TD', symbol: 'NOVO.TO', companyName: 'Novo Nordisk', role: 'Swing', averageCost: 17, currentPrice: 18,
  priceBasis: 'EOD', priceAsOfUtc: '2026-10-28T20:00:00Z', marketValue: 10800, unrealizedPnl: 600, unrealizedPnlPct: 5.9,
};

describe('ManageRiskDialogComponent', () => {
  let fixture: ComponentFixture<ManageRiskDialogComponent>;
  let pos: ReturnType<typeof signal<RiskPosition | null>>;
  let store: Record<string, unknown>;
  let close: ReturnType<typeof vi.fn>;

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const q = <T extends Element>(sel: string): T | null => (fixture.nativeElement as HTMLElement).querySelector<T>(sel);
  const setInput = (sel: string, value: string): void => {
    const el = q<HTMLInputElement>(sel)!;
    el.value = value;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  };

  async function openTab(label: string): Promise<void> {
    const tabs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('[role="tab"]'));
    tabs.find((t) => t.textContent!.includes(label))!.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function create(p: RiskPosition | null = position()): Promise<void> {
    pos = signal<RiskPosition | null>(p);
    close = vi.fn();
    store = {
      position: pos,
      positionLoading: signal(false),
      positionError: signal<string | null>(null),
      conflict: signal(false),
      saving: signal(false),
      loadPosition: vi.fn(),
      clearPosition: vi.fn(),
      savePlan: vi.fn(() => Promise.resolve(true)),
      saveBrokerStop: vi.fn(() => Promise.resolve(true)),
      transitionBrokerStop: vi.fn(() => Promise.resolve(true)),
    };
    await TestBed.configureTestingModule({
      imports: [ManageRiskDialogComponent],
      providers: [
        { provide: SwingTradingStore, useValue: store },
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ManageRiskDialogComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads the position for the account and symbol and shows the read-only position facts', async () => {
    await create();
    expect(store['loadPosition']).toHaveBeenCalledWith({ account: 'TFSA_D_TD', symbol: 'NOVO.TO' });
    expect(text()).toContain('NOVO.TO');
    expect(text()).toContain('TFSA_D_TD');
    expect(text()).toContain('EOD close');
    expect(text()).toContain('never places, changes or cancels an order');
  });

  it('shows an unknown covered quantity as Unknown, not zero, and flags coverage as unknown', async () => {
    await create();
    await openTab('Broker stops');
    expect(q('[data-testid="mr-coverage"]')!.textContent).toContain('Unknown');
    const stopCard = q('[data-testid="mr-stop"]')!.textContent!;
    expect(stopCard).toContain('Unknown');
    expect(stopCard).toContain('Not broker-verified');
    expect(stopCard).not.toContain('Broker-verified ');
  });

  it('blocks saving a non-positive planned stop and explains why', async () => {
    await create();
    setInput('[data-testid="mr-plan-stop"]', '-5');
    expect(q<HTMLButtonElement>('[data-testid="mr-plan-save"]')!.disabled).toBe(true);
    expect(text()).toContain('must be a positive price');
  });

  it('warns but allows a long stop at/above the latest price', async () => {
    await create();
    setInput('[data-testid="mr-plan-stop"]', '18.5');
    expect(q<HTMLButtonElement>('[data-testid="mr-plan-save"]')!.disabled).toBe(false);
    expect(q('[data-testid="mr-plan-warnings"]')!.textContent).toContain('at or above the latest price');
  });

  it('saves the plan with the exact values and says no broker order changed', async () => {
    await create();
    setInput('[data-testid="mr-plan-stop"]', '15.75');
    setInput('[data-testid="mr-plan-target"]', '21');
    (q<HTMLFormElement>('form[aria-label="Risk plan"]')!).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(store['savePlan']).toHaveBeenCalledWith(
      expect.objectContaining({ accountType: 'TFSA_D_TD', symbol: 'NOVO.TO', plannedStopPrice: 15.75, targetPrice: 21, stopType: 'Fixed', rowVersion: null }),
    );
    expect(q('[data-testid="mr-saved"]')!.textContent).toContain('No broker order was changed');
  });

  it('shows both stops and a mismatch note when the plan differs from the recorded broker stop', async () => {
    await create(
      position({
        plan: {
          id: 1, accountType: 'TFSA_D_TD', symbol: 'NOVO.TO', plannedStopPrice: 15.5, targetPrice: null, stopType: 'Fixed',
          stopRationale: null, tradeRationale: null, riskNotes: null, currency: 'CAD', episodeOpenDate: '2026-10-15T00:00:00',
          belongsToCurrentEpisode: true, updatedAtUtc: '', rowVersion: 'p1',
        },
      }),
    );
    expect(q('[data-testid="mr-mismatch"]')!.textContent).toContain('does not change the broker order');
    expect(q<HTMLInputElement>('[data-testid="mr-plan-stop"]')!.value).toBe('15.5');
  });

  it('records a new broker stop but rejects over-coverage until it is explained', async () => {
    await create(position({ brokerStops: [] }));
    await openTab('Broker stops');
    expect(q('[data-testid="mr-no-stops"]')).not.toBeNull();
    (q<HTMLButtonElement>('[data-testid="mr-add-stop"]')!).click();
    fixture.detectChanges();
    setInput('[data-testid="mr-stop-trigger"]', '16.2');
    setInput('[data-testid="mr-stop-covered"]', '900');
    expect(q<HTMLButtonElement>('[data-testid="mr-stop-save"]')!.disabled).toBe(true);
    expect(q('[data-testid="mr-stop-covered-err"]')!.textContent).toContain('Exceeds the account quantity');

    setInput('[data-testid="mr-stop-covered"]', '600');
    expect(q<HTMLButtonElement>('[data-testid="mr-stop-save"]')!.disabled).toBe(false);
    (q<HTMLFormElement>('[data-testid="mr-stop-form"]')!).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(store['saveBrokerStop']).toHaveBeenCalledWith(
      expect.objectContaining({ id: null, triggerPrice: 16.2, coveredQuantity: 600, orderType: 'StopMarket', allowOverCoverage: false }),
    );
  });

  it('leaves covered quantity null when blank (unknown), never 0', async () => {
    await create(position({ brokerStops: [] }));
    await openTab('Broker stops');
    (q<HTMLButtonElement>('[data-testid="mr-add-stop"]')!).click();
    fixture.detectChanges();
    setInput('[data-testid="mr-stop-trigger"]', '16.2');
    (q<HTMLFormElement>('[data-testid="mr-stop-form"]')!).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(store['saveBrokerStop']).toHaveBeenCalledWith(expect.objectContaining({ coveredQuantity: null }));
  });

  it('records "it filled" as a user report through a transition panel', async () => {
    await create();
    await openTab('Broker stops');
    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.mr-row-actions button'));
    buttons.find((b) => b.textContent!.includes('It filled'))!.click();
    fixture.detectChanges();
    expect(q('[data-testid="mr-transition"]')!.textContent).toContain('not verified with the broker');
    (q<HTMLButtonElement>('[data-testid="mr-transition-confirm"]')!).click();
    await fixture.whenStable();
    expect(store['transitionBrokerStop']).toHaveBeenCalledWith(1, expect.objectContaining({ status: 'UserReportedFilled', rowVersion: 'v1' }));
  });

  it('shows a reload action after a concurrency conflict', async () => {
    await create();
    (store['positionError'] as ReturnType<typeof signal<string | null>>).set('The risk plan was changed by someone else.');
    (store['conflict'] as ReturnType<typeof signal<boolean>>).set(true);
    fixture.detectChanges();
    expect(q('[data-testid="mr-error"]')!.textContent).toContain('changed by someone else');
    const reload = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find((b) => b.textContent!.includes('Reload latest'))!;
    reload.click();
    expect(store['loadPosition']).toHaveBeenCalledTimes(2);
  });

  it('warns that a closed trade\'s broker order may still be open and does not count it as active', async () => {
    await create(position({ brokerStops: [stop({ isActive: false, userStatus: 'EpisodeClosed', brokerCancellationUnverified: true, belongsToCurrentEpisode: false })] }));
    await openTab('Broker stops');
    expect(q('[data-testid="mr-closed-warning"]')!.textContent).toContain('may still be open');
    expect(q('[data-testid="mr-no-stops"]')).not.toBeNull();
    expect(q('[data-testid="mr-coverage"]')!.textContent).toContain('None');
  });

  it('close clears the shown position and closes the dialog without saving', async () => {
    await create();
    (q<HTMLButtonElement>('[data-testid="mr-close"]')!).click();
    expect(store['clearPosition']).toHaveBeenCalled();
    expect(close).toHaveBeenCalled();
    expect(store['savePlan']).not.toHaveBeenCalled();
  });

  it('lists the audit history with old and new values', async () => {
    await create(position({
      history: [{ id: 5, occurredAtUtc: '2026-10-28T10:00:00Z', userName: 'dima', source: 'User', eventType: 'PlanUpdated',
        changes: [{ field: 'PlannedStopPrice', old: '9.5', new: '9.25' }], reason: 'tighten', brokerStopRecordId: null }],
    }));
    const tabs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('[role="tab"]'));
    tabs.find((t) => t.textContent!.includes('History'))!.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(q('[data-testid="mr-history"]')!.textContent).toContain('PlannedStopPrice: 9.5 → 9.25');
    expect(q('[data-testid="mr-history"]')!.textContent).toContain('tighten');
  });
});
