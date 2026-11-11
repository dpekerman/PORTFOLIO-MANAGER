import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { BrokerStop, ManageRiskDialogData, RiskPosition } from '../../core/models/swing-trading.models';
import { SwingTradingStore } from '../../core/store/swing-trading.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
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
    const header = q<HTMLElement>('[data-testid="mr-advanced"] .mat-expansion-panel-header');
    if (header && header.getAttribute('aria-expanded') !== 'true') {
      header.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    }
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

  it('PM033 finding 12: demo mode masks the read-only average cost', async () => {
    await create();
    const demo = TestBed.inject(DemoModeStore);
    demo.enable();
    try {
      await fixture.whenStable();
      const label = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('dt'))
        .find((element) => element.textContent?.trim() === 'Avg. cost');
      expect(label).toBeDefined();
      expect(label!.nextElementSibling!.textContent?.trim()).toBe(
        demo.maskValue(data.averageCost!).toFixed(2),
      );
      expect(q('[data-testid="mr-demo-readonly"]')).not.toBeNull();
      expect(q('[data-testid="mr-save"]')).toBeNull();
      expect((fixture.nativeElement as HTMLElement).querySelectorAll('input')).toHaveLength(0);
    } finally {
      demo.disable();
    }
  });

  it('uses authoritative API account and Swing facts instead of conflicting caller values', async () => {
    await create(position({
      mixedRole: true, totalQuantity: 200, swingQuantity: 100,
      facts: {
        averageCost: 41, swingAverageCost: 30, currentPrice: 50, priceBasis: 'Stale',
        priceAsOfUtc: null, marketValue: 10000, unrealizedPnl: 1800, unrealizedPnlPct: 21.95,
        swingMarketValue: 5000, swingUnrealizedPnl: 2000,
      },
    }));
    const valueFor = (name: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('dt'))
      .find((element) => element.textContent?.trim() === name)?.nextElementSibling?.textContent?.trim();
    expect(valueFor('Avg. cost')).toBe('41.00');
    expect(valueFor('Swing average cost')).toBe('30.00');
    expect(valueFor('Market value')).toBe('10,000.00 CAD');
    expect(valueFor('Swing market value')).toBe('5,000.00');
    expect(valueFor('Current price')).toContain('50.00');
    expect(valueFor('Current price')).toContain('Stale');
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
    expect(q<HTMLButtonElement>('[data-testid="mr-save"]')!.disabled).toBe(true);
    expect(text()).toContain('must be a positive price');
  });

  it('warns but allows a long stop at/above the latest price', async () => {
    await create();
    setInput('[data-testid="mr-plan-stop"]', '18.5');
    expect(q<HTMLButtonElement>('[data-testid="mr-save"]')!.disabled).toBe(false);
    expect(q('[data-testid="mr-plan-warnings"]')!.textContent).toContain('at or above the latest price');
  });

  it('PM-032: Trade Plan (Planned Stop and Target) comes first and shows To Stop, To Target and R:R', async () => {
    await create(position({ brokerStops: [] }));
    expect(q('[data-testid="mr-trade-plan"]')).not.toBeNull();
    expect(q('[data-testid="mr-advanced"] .mat-expansion-panel-header')!.getAttribute('aria-expanded')).toBe('false');
    expect(q('[data-testid="mr-quick-trigger"]')).toBeNull();
    setInput('[data-testid="mr-plan-stop"]', '17.1');
    setInput('[data-testid="mr-plan-target"]', '19.8');
    expect(q('[data-testid="mr-to-stop"]')!.textContent).toContain('-5.0%');
    expect(q('[data-testid="mr-to-target"]')!.textContent).toContain('+10.0%');
    expect(q('[data-testid="mr-rr"]')!.textContent).toContain('1 : 2.00');
  });

  it('PM-032: the broker confirmation defaults trigger and limit from the planned stop and needs the checkbox', async () => {
    await create(position({ brokerStops: [] }));
    const save = () => q<HTMLButtonElement>('[data-testid="mr-save"]')!;
    setInput('[data-testid="mr-plan-stop"]', '16');
    q<HTMLButtonElement>('[data-testid="mr-edit-broker"]')!.click();
    fixture.detectChanges();
    expect(q<HTMLInputElement>('[data-testid="mr-quick-trigger"]')!.value).toBe('16');
    expect(q<HTMLInputElement>('[data-testid="mr-quick-limit"]')!.value).toBe('16');
    expect(q<HTMLInputElement>('[data-testid="mr-quick-shares"]')!.value).toBe('600');
    setInput('[data-testid="mr-quick-limit"]', '16.5');
    expect(q('[data-testid="mr-quick-limit-err"]')!.textContent).toContain('must not exceed the stop trigger');
    q<HTMLInputElement>('[data-testid="mr-quick-confirm"] input')!.click();
    fixture.detectChanges();
    expect(save().disabled).toBe(true);
    setInput('[data-testid="mr-quick-limit"]', '15.8');
    expect(save().disabled).toBe(false);
  });

  it('PM-032: Save writes the plan and a user-reported StopLimit with the confirmation time', async () => {
    await create(position({ brokerStops: [] }));
    setInput('[data-testid="mr-plan-stop"]', '16');
    setInput('[data-testid="mr-plan-target"]', '20');
    q<HTMLInputElement>('[data-testid="mr-quick-confirm"] input')!.click();
    fixture.detectChanges();
    q<HTMLButtonElement>('[data-testid="mr-save"]')!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(store['savePlan']).toHaveBeenCalledWith(expect.objectContaining({ plannedStopPrice: 16, targetPrice: 20 }));
    expect(store['saveBrokerStop']).toHaveBeenCalledWith(expect.objectContaining({
      id: null, orderType: 'StopLimit', triggerPrice: 16, limitPrice: 16, coveredQuantity: 600, allowOverCoverage: false,
    }));
    const request = (store['saveBrokerStop'] as ReturnType<typeof vi.fn>).mock.calls[0][0] as { lastUserVerifiedDate: string };
    expect(Number.isNaN(Date.parse(request.lastUserVerifiedDate))).toBe(false);
    expect(q('[data-testid="mr-saved"]')!.textContent).toContain('This application does not verify, place or execute broker orders.');
  });

  it('PM-032: without the checkbox only the plan is saved and no broker order is written', async () => {
    await create(position({ brokerStops: [] }));
    setInput('[data-testid="mr-plan-stop"]', '16');
    q<HTMLButtonElement>('[data-testid="mr-save"]')!.click();
    await fixture.whenStable();
    expect(store['savePlan']).toHaveBeenCalled();
    expect(store['saveBrokerStop']).not.toHaveBeenCalled();
  });

  it('PM-032: with several active orders the user must choose one; nothing is edited implicitly', async () => {
    await create(position({
      brokerStops: [
        stop({ id: 1, orderType: 'StopLimit', limitPrice: 15.5, coveredQuantity: 300 }),
        stop({ id: 2, orderType: 'StopLimit', limitPrice: 15.6, coveredQuantity: 300, rowVersion: 'v2' }),
      ],
    }));
    expect(q('[data-testid="mr-quick-choose"]')).not.toBeNull();
    expect(q('[data-testid="mr-quick-form"]')).toBeNull();
    (fixture.componentInstance as unknown as { chooseQuick(id: number): void }).chooseQuick(2);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(q('[data-testid="mr-quick-form"]')).not.toBeNull();
    expect(q<HTMLInputElement>('[data-testid="mr-quick-limit"]')!.value).toBe('15.6');
    expect(q<HTMLInputElement>('[data-testid="mr-quick-confirm"] input')!.checked).toBe(false);
  });

  it('PM-032: a single existing order is summarised, never auto-confirmed, and Save stays disabled until the user confirms', async () => {
    await create(position({ brokerStops: [stop({ id: 7, orderType: 'StopLimit', limitPrice: 15.5, coveredQuantity: 600 })] }));
    expect(q('[data-testid="mr-order-summary"]')!.textContent).toContain('Trigger: 16.2');
    expect(q('[data-testid="mr-order-summary"]')!.textContent).toContain('Limit: 15.5');
    expect(q<HTMLInputElement>('[data-testid="mr-quick-confirm"] input')!.checked).toBe(false);
    expect(q('[data-testid="mr-confirm-chip"]')!.textContent).toContain('Not confirmed');
    expect(q<HTMLButtonElement>('[data-testid="mr-save"]')!.disabled).toBe(true);
    q<HTMLInputElement>('[data-testid="mr-quick-confirm"] input')!.click();
    fixture.detectChanges();
    expect(q<HTMLButtonElement>('[data-testid="mr-save"]')!.disabled).toBe(false);
  });

  it('PM-032: an already self-confirmed, unchanged order shows Self-confirmed and writes nothing', async () => {
    await create(position({ brokerStops: [stop({ id: 7, orderType: 'StopLimit', limitPrice: 15.5, coveredQuantity: 600, lastUserVerifiedDate: '2026-11-11T10:00:00Z' })] }));
    expect(q('[data-testid="mr-confirm-chip"]')!.textContent).toContain('Self-confirmed');
    expect(q<HTMLButtonElement>('[data-testid="mr-save"]')!.disabled).toBe(true);
  });

  it('PM-032: partial close shows the exact quantity mismatch message', async () => {
    await create(position({
      totalQuantity: 1000, swingQuantity: 1000,
      brokerStops: [stop({ orderType: 'StopLimit', limitPrice: 15.5, coveredQuantity: 1500 })],
    }));
    expect(q('[data-testid="mr-overcoverage"]')!.textContent).toContain('Order covers 1,500 shares; 1,000 remain open. Verify or amend at your broker.');
  });

  it('saves the plan with the exact values and says no broker order changed', async () => {
    await create();
    setInput('[data-testid="mr-plan-stop"]', '15.75');
    setInput('[data-testid="mr-plan-target"]', '21');
    q<HTMLButtonElement>('[data-testid="mr-save"]')!.click();
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
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('mat-datepicker-toggle').length).toBe(2);
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

  it('opens a calendar from each broker-stop date field', async () => {
    await create(position({ brokerStops: [] }));
    await openTab('Broker stops');
    (q<HTMLButtonElement>('[data-testid="mr-add-stop"]')!).click();
    fixture.detectChanges();

    const toggles = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
      'mat-datepicker-toggle button',
    );
    expect(toggles).toHaveLength(2);
    toggles[0].click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.body.querySelector('[role="grid"]')).not.toBeNull();
    toggles[0].click();
    fixture.detectChanges();
    await fixture.whenStable();
    toggles[1].click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.body.querySelector('[role="grid"]')).not.toBeNull();
  });

  it('saves calendar dates as date-only values without shifting the day', async () => {
    await create(position({ brokerStops: [] }));
    await openTab('Broker stops');
    (q<HTMLButtonElement>('[data-testid="mr-add-stop"]')!).click();
    fixture.detectChanges();
    setInput('[data-testid="mr-stop-trigger"]', '16.2');
    setInput('[data-testid="mr-stop-placed-date"]', '10/28/2026');
    setInput('[data-testid="mr-stop-verified-date"]', '10/29/2026');
    (q<HTMLFormElement>('[data-testid="mr-stop-form"]')!).dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(store['saveBrokerStop']).toHaveBeenCalledWith(
      expect.objectContaining({ placedDate: '2026-10-28', lastUserVerifiedDate: '2026-10-29' }),
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
    expect(q<HTMLButtonElement>('[data-testid="mr-transition-confirm"]')!.disabled).toBe(true);
    setInput('[data-testid="mr-transition"] input', 'Confirmed on broker statement');
    fixture.detectChanges();
    (q<HTMLButtonElement>('[data-testid="mr-transition-confirm"]')!).click();
    await fixture.whenStable();
    expect(store['transitionBrokerStop']).toHaveBeenCalledWith(1, expect.objectContaining({
      status: 'UserReportedFilled', rowVersion: 'v1', reason: 'Confirmed on broker statement',
    }));
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
