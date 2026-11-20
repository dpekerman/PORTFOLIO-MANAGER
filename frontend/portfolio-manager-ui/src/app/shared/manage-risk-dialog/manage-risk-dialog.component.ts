import { DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { form, FormField } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatRadioModule } from '@angular/material/radio';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import {
  BrokerOrderType,
  BrokerStop,
  ManageRiskDialogData,
  RiskPosition,
  StopType,
} from '../../core/models/swing-trading.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { SwingTradingStore } from '../../core/store/swing-trading.store';
import {
  COVERAGE_LABELS,
  brokerStopStatusLabel,
  hasErrors,
  overCoverageMessage,
  priceBasisLabel,
  selfConfirmationLabel,
  summarizeCoverage,
  validateBrokerStop,
  validateQuickStopLimit,
  validateRiskPlan,
} from '../../features/swing-trading/swing-trading-logic';

type TransitionStatus = 'UserReportedCancelled' | 'UserReportedFilled' | 'Superseded';

interface QuickModel {
  triggerPrice: number | null;
  limitPrice: number | null;
  coveredQuantity: number | null;
  confirmed: boolean;
}

interface PlanModel {
  plannedStopPrice: number | null;
  targetPrice: number | null;
  stopType: StopType;
  stopRationale: string;
  tradeRationale: string;
  riskNotes: string;
  reason: string;
}

interface StopModel {
  orderType: BrokerOrderType;
  triggerPrice: number | null;
  limitPrice: number | null;
  coveredQuantity: number | null;
  placedDate: Date | null;
  brokerOrderRef: string;
  lastUserVerifiedDate: Date | null;
  notes: string;
  reason: string;
  allowOverCoverage: boolean;
}

function dateFromString(value: string | null): Date | null {
  if (!value) return null;
  const [year, month, day] = value.substring(0, 10).split('-').map(Number);
  return new Date(year, month - 1, day);
}

function dateToString(value: Date | null): string | null {
  if (!value) return null;
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

const emptyStop = (): StopModel => ({
  orderType: 'StopMarket',
  triggerPrice: null,
  limitPrice: null,
  coveredQuantity: null,
  placedDate: null,
  brokerOrderRef: '',
  lastUserVerifiedDate: null,
  notes: '',
  reason: '',
  allowOverCoverage: false,
});

/** Opens the one shared Manage Risk dialog. Phones get the app-wide near-full-width dialog sizing (see styles.scss). */
export function openManageRiskDialog(dialog: MatDialog, data: ManageRiskDialogData) {
  return dialog.open(ManageRiskDialogComponent, {
    data,
    autoFocus: 'first-heading',
    width: '840px',
    maxWidth: '95vw',
    maxHeight: '95dvh',
  });
}

/**
 * Shared Manage Risk dialog (Swing Trading and Portfolio). It edits two DISTINCT things: the planned stop/target
 * (what the user intends) and the broker orders the user says they placed. Changing the plan never changes a
 * recorded order, and nothing here contacts a brokerage.
 */
@Component({
  selector: 'app-manage-risk-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormField,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatDialogModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
    MatRadioModule,
    MatSelectModule,
    MatTabsModule,
  ],
  templateUrl: './manage-risk-dialog.component.html',
  styleUrl: './manage-risk-dialog.component.scss',
})
export class ManageRiskDialogComponent {
  protected readonly data = inject<ManageRiskDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ManageRiskDialogComponent>);
  protected readonly store = inject(SwingTradingStore);
  protected readonly demo = inject(DemoModeStore);

  protected readonly coverageLabels = COVERAGE_LABELS;
  protected readonly orderTypes: BrokerOrderType[] = ['StopMarket', 'StopLimit', 'Other'];
  protected readonly stopTypes: StopType[] = ['Fixed', 'Trailing'];
  protected readonly selectedTab = signal(0);

  protected readonly position = this.store.position;
  protected readonly display = computed<ManageRiskDialogData>(() => {
    const facts = this.position()?.facts;
    return facts ? { ...this.data, ...facts } : this.data;
  });
  protected readonly priceUsable = computed(
    () => this.display().currentPrice != null && (this.display().priceBasis === 'Live' || this.display().priceBasis === 'EOD'),
  );

  // ── Risk plan form ─────────────────────────────────────────────────────────────────────
  protected readonly planModel = signal<PlanModel>({
    plannedStopPrice: null,
    targetPrice: null,
    stopType: 'Fixed',
    stopRationale: '',
    tradeRationale: '',
    riskNotes: '',
    reason: '',
  });
  protected readonly planForm = form(this.planModel);
  protected readonly planValidation = computed(() =>
    validateRiskPlan(this.planModel(), {
      currentPrice: this.display().currentPrice ?? null,
      priceUsable: this.priceUsable(),
    }),
  );
  protected readonly planInvalid = computed(() => hasErrors(this.planValidation()));

  // ── Broker stop form ───────────────────────────────────────────────────────────────────
  protected readonly stopModel = signal<StopModel>(emptyStop());
  protected readonly stopForm = form(this.stopModel);
  /** null = not editing, 0 = adding a new record, otherwise the id being edited. */
  protected readonly editingStopId = signal<number | null>(null);
  protected readonly editingStopVersion = signal<string | null>(null);
  protected readonly stopValidation = computed(() =>
    validateBrokerStop(this.stopModel(), {
      totalQuantity: this.position()?.totalQuantity ?? 0,
      currentPrice: this.display().currentPrice ?? null,
      priceUsable: this.priceUsable(),
    }),
  );
  protected readonly stopInvalid = computed(() => hasErrors(this.stopValidation()));

  // ── Quick stop-limit card (default interaction) ─────────────────────────────────────────
  protected readonly quickModel = signal<QuickModel>({ triggerPrice: null, limitPrice: null, coveredQuantity: null, confirmed: false });
  protected readonly quickForm = form(this.quickModel);
  /** Explicit user choice: an order id to amend, 0 for a new separate order, null = not chosen yet. */
  protected readonly quickChoice = signal<number | null>(null);
  protected readonly advancedOpen = signal(false);
  /**
   * Which order the quick card edits. A lone StopLimit order opens for amendment; with several orders (or only a
   * legacy non-StopLimit one) the user must choose, and nothing is ever overwritten or merged implicitly.
   */
  protected readonly quickTarget = computed<number | null>(() => {
    const choice = this.quickChoice();
    if (choice !== null) return choice;
    const active = this.activeStops();
    if (active.length === 0) return 0;
    if (active.length === 1 && active[0].orderType === 'StopLimit') return active[0].id;
    return null;
  });
  protected readonly quickTargetStop = computed(() => this.activeStops().find((s) => s.id === this.quickTarget()) ?? null);
  protected readonly otherCovered = computed(() =>
    this.activeStops()
      .filter((s) => s.id !== this.quickTarget())
      .reduce((sum, s) => sum + (s.coveredQuantity ?? 0), 0),
  );
  protected readonly quickValidation = computed(() =>
    validateQuickStopLimit(this.quickModel(), {
      totalQuantity: this.position()?.totalQuantity ?? 0,
      otherCovered: this.otherCovered(),
      currentPrice: this.display().currentPrice ?? null,
      priceUsable: this.priceUsable(),
    }),
  );
  protected readonly quickInvalid = computed(() => hasErrors(this.quickValidation()));
  protected readonly overCoverage = computed(() => {
    const c = this.coverage();
    const p = this.position();
    return c?.state === 'OverCovered' && c.covered !== null && p ? overCoverageMessage(c.covered, p.totalQuantity) : null;
  });
  private quickHydratedKey: string | null = null;
  protected readonly quickTouched = signal(false);
  protected readonly editingBroker = signal(false);
  private planBaseline = signal<string>('');

  /** Distances from the latest price to the planned stop and target, and the resulting risk/reward. */
  protected readonly metrics = computed(() => {
    const price = this.display().currentPrice ?? null;
    const { plannedStopPrice: stop, targetPrice: target } = this.planModel();
    const ok = (v: number | null): v is number => v !== null && !Number.isNaN(v) && v > 0;
    if (price === null || !(price > 0)) return { toStop: null, toTarget: null, rr: null };
    const toStop = ok(stop) ? ((stop - price) / price) * 100 : null;
    const toTarget = ok(target) ? ((target - price) / price) * 100 : null;
    const rr = ok(stop) && ok(target) && price > stop && target > price ? (target - price) / (price - stop) : null;
    return { toStop, toTarget, rr };
  });
  private readonly brokerChanged = computed(() => {
    const s = this.quickTargetStop();
    const m = this.quickModel();
    return !!s && (s.triggerPrice !== m.triggerPrice || s.limitPrice !== m.limitPrice || s.coveredQuantity !== m.coveredQuantity);
  });
  protected readonly selfConfirmed = computed(() => {
    const s = this.quickTargetStop();
    return !!s && !!s.lastUserVerifiedDate && !this.brokerChanged() && this.quickModel().confirmed;
  });
  protected readonly showBrokerFields = computed(() => {
    if (this.editingBroker() || this.quickTarget() === null || this.quickChoice() !== null) return true;
    const e = this.quickValidation().errors;
    return this.quickModel().confirmed && !!(e['triggerPrice'] || e['limitPrice'] || e['coveredQuantity']);
  });
  protected readonly planDirty = computed(() => JSON.stringify(this.planModel()) !== this.planBaseline());
  /** Only an explicit, ticked confirmation ever writes a broker order; an unchanged self-confirmed order writes nothing. */
  protected readonly brokerWriteNeeded = computed(() => {
    const m = this.quickModel();
    if (!m.confirmed || this.quickTarget() === null) return false;
    const s = this.quickTargetStop();
    return s === null || this.brokerChanged() || !s.lastUserVerifiedDate;
  });
  protected readonly canSave = computed(() => {
    if (!this.position()?.hasOpenPosition) return false;
    const plan = this.planDirty();
    const broker = this.brokerWriteNeeded();
    return (plan || broker) && !this.planInvalid() && (!broker || !this.quickInvalid());
  });

  // ── Status change (cancelled / filled / replaced) ───────────────────────────────────────
  protected readonly transition = signal<{ stop: BrokerStop; status: TransitionStatus } | null>(null);
  protected readonly transitionReason = signal('');
  protected readonly replacedBy = signal<number | null>(null);

  protected readonly savedMessage = signal('');

  protected readonly coverage = computed(() => {
    const p = this.position();
    return p ? summarizeCoverage(p) : null;
  });
  protected readonly activeStops = computed(() => this.coverage()?.active ?? []);
  protected readonly otherActiveStops = computed(() =>
    this.activeStops().filter((s) => s.id !== this.transition()?.stop.id),
  );
  /** The planned stop and a recorded broker order disagree: both stay visible, neither is changed for the other. */
  protected readonly mismatch = computed(() => {
    const planned = this.position()?.plan?.belongsToCurrentEpisode ? this.position()!.plan!.plannedStopPrice : null;
    if (planned === null) return null;
    const differing = this.activeStops().find((s) => s.triggerPrice !== planned);
    return differing ? { planned, broker: differing.triggerPrice } : null;
  });
  protected readonly planFromPriorEpisode = computed(() => {
    const plan = this.position()?.plan;
    return !!plan && !plan.belongsToCurrentEpisode;
  });
  protected readonly priorStops = computed(() =>
    (this.position()?.brokerStops ?? []).filter((s) => !s.isActive || !s.belongsToCurrentEpisode),
  );
  protected readonly retiredNeedingCheck = computed(() =>
    this.priorStops().filter((s) => s.brokerCancellationUnverified),
  );

  private hydratedVersion: string | null | undefined = undefined;

  constructor() {
    this.store.loadPosition({ account: this.data.account, symbol: this.data.symbol });
    effect(() => {
      const p = this.position();
      if (!p) return;
      untracked(() => this.hydratePlan(p));
    });
    effect(() => {
      const p = this.position();
      const target = this.quickTarget();
      const stop = this.quickTargetStop();
      if (!p || target === null) return;
      untracked(() => this.hydrateQuick(p.totalQuantity, target, stop));
    });
    effect(() => {
      const stop = this.planModel().plannedStopPrice;
      const target = this.quickTarget();
      const touched = this.quickTouched();
      if (target !== 0 || touched || stop === null || !(stop > 0)) return;
      untracked(() => this.quickModel.update((m) => ({ ...m, triggerPrice: stop, limitPrice: stop })));
    });
  }

  private hydrateQuick(total: number, target: number, stop: BrokerStop | null): void {
    const key = `${target}:${stop?.rowVersion ?? ''}:${this.activeStops().length}`;
    if (this.quickHydratedKey === key) return;
    this.quickHydratedKey = key;
    this.quickTouched.set(false);
    if (stop) {
      this.quickModel.set({ triggerPrice: stop.triggerPrice, limitPrice: stop.limitPrice, coveredQuantity: stop.coveredQuantity, confirmed: !!stop.lastUserVerifiedDate });
      return;
    }
    const remaining = total - this.otherCovered();
    const planned = this.planModel().plannedStopPrice;
    this.quickModel.set({
      triggerPrice: planned,
      limitPrice: planned,
      coveredQuantity: remaining > 0 ? remaining : null,
      confirmed: false,
    });
  }

  protected chooseQuick(id: number): void {
    this.quickChoice.set(id);
    this.savedMessage.set('');
  }

  protected toggleBrokerEdit(): void {
    this.editingBroker.update((v) => !v);
    if (this.editingBroker()) this.quickTouched.set(true);
  }

  protected async save(): Promise<void> {
    if (!this.canSave()) return;
    const plan = this.planDirty();
    const broker = this.brokerWriteNeeded();
    if (plan && !(await this.persistPlan())) return;
    if (broker && !(await this.persistBroker())) return;
    const brokerMsg = 'Broker stop-limit self-confirmed. This application does not verify, place or execute broker orders.';
    this.savedMessage.set(
      plan && broker ? `Risk plan saved. ${brokerMsg}` : broker ? brokerMsg : 'Risk plan saved. No broker order was changed.',
    );
  }

  private async persistBroker(): Promise<boolean> {
    const target = this.quickTarget();
    if (target === null || this.quickInvalid()) return false;
    const m = this.quickModel();
    const existing = this.quickTargetStop();
    const saved = await this.store.saveBrokerStop({
      id: target === 0 ? null : target,
      accountType: this.data.account,
      symbol: this.data.symbol,
      orderType: 'StopLimit',
      triggerPrice: m.triggerPrice,
      limitPrice: m.limitPrice,
      coveredQuantity: m.coveredQuantity,
      placedDate: existing?.placedDate ?? null,
      brokerOrderRef: existing?.brokerOrderRef ?? null,
      lastUserVerifiedDate: new Date().toISOString(),
      notes: existing?.notes ?? null,
      reason: null,
      allowOverCoverage: false,
      rowVersion: existing?.rowVersion ?? null,
    });
    if (saved) {
      this.quickChoice.set(null);
      this.quickHydratedKey = null;
      this.editingBroker.set(false);
    }
    return saved;
  }

  protected basisLabel(): string {
    return priceBasisLabel(this.display().priceBasis ?? 'Unavailable');
  }

  protected statusLabel(s: BrokerStop): string {
    return brokerStopStatusLabel(s);
  }

  protected confirmLabel(s: BrokerStop): string {
    return selfConfirmationLabel(s);
  }

  protected value(n: number | null | undefined): number | null {
    return n == null ? null : this.demo.maskValue(n);
  }

  protected pct(n: number | null | undefined): number | null {
    return n == null ? null : this.demo.maskPercent(n);
  }

  private hydratePlan(p: RiskPosition): void {
    const plan = p.plan?.belongsToCurrentEpisode ? p.plan : null;
    const version = plan?.rowVersion ?? null;
    if (this.hydratedVersion === version) return;
    this.hydratedVersion = version;
    this.planModel.set({
      plannedStopPrice: plan?.plannedStopPrice ?? null,
      targetPrice: plan?.targetPrice ?? null,
      stopType: plan?.stopType ?? 'Fixed',
      stopRationale: plan?.stopRationale ?? '',
      tradeRationale: plan?.tradeRationale ?? '',
      riskNotes: plan?.riskNotes ?? '',
      reason: '',
    });
    this.planBaseline.set(JSON.stringify(this.planModel()));
  }

  private async persistPlan(): Promise<boolean> {
    if (this.planInvalid()) return false;
    const m = this.planModel();
    const plan = this.position()?.plan;
    return this.store.savePlan({
      accountType: this.data.account,
      symbol: this.data.symbol,
      plannedStopPrice: m.plannedStopPrice,
      targetPrice: m.targetPrice,
      stopType: m.stopType,
      stopRationale: m.stopRationale.trim() || null,
      tradeRationale: m.tradeRationale.trim() || null,
      riskNotes: m.riskNotes.trim() || null,
      reason: m.reason.trim() || null,
      rowVersion: plan?.rowVersion ?? null,
    });
  }

  protected startAddStop(): void {
    this.stopModel.set(emptyStop());
    this.editingStopVersion.set(null);
    this.editingStopId.set(0);
    this.savedMessage.set('');
  }

  protected startEditStop(s: BrokerStop): void {
    this.stopModel.set({
      orderType: s.orderType,
      triggerPrice: s.triggerPrice,
      limitPrice: s.limitPrice,
      coveredQuantity: s.coveredQuantity,
      placedDate: dateFromString(s.placedDate),
      brokerOrderRef: s.brokerOrderRef ?? '',
      lastUserVerifiedDate: dateFromString(s.lastUserVerifiedDate),
      notes: s.notes ?? '',
      reason: '',
      allowOverCoverage: false,
    });
    this.editingStopVersion.set(s.rowVersion);
    this.editingStopId.set(s.id);
    this.savedMessage.set('');
  }

  protected cancelStopEdit(): void {
    this.editingStopId.set(null);
    this.editingStopVersion.set(null);
  }

  protected async saveStop(): Promise<void> {
    const id = this.editingStopId();
    if (id === null || this.stopInvalid()) return;
    const m = this.stopModel();
    const saved = await this.store.saveBrokerStop({
      id: id === 0 ? null : id,
      accountType: this.data.account,
      symbol: this.data.symbol,
      orderType: m.orderType,
      triggerPrice: m.triggerPrice,
      limitPrice: m.orderType === 'StopLimit' ? m.limitPrice : null,
      coveredQuantity: m.coveredQuantity,
      placedDate: dateToString(m.placedDate),
      brokerOrderRef: m.brokerOrderRef.trim() || null,
      lastUserVerifiedDate: dateToString(m.lastUserVerifiedDate),
      notes: m.notes.trim() || null,
      reason: m.reason.trim() || null,
      allowOverCoverage: m.allowOverCoverage,
      rowVersion: id === 0 ? null : this.editingStopVersion(),
    });
    if (saved) {
      this.cancelStopEdit();
      this.savedMessage.set('Broker stop record saved. This records what you did at your broker; it did not place or change an order.');
    }
  }

  protected beginTransition(stop: BrokerStop, status: TransitionStatus): void {
    this.transition.set({ stop, status });
    this.transitionReason.set('');
    this.replacedBy.set(null);
  }

  protected cancelTransition(): void {
    this.transition.set(null);
  }

  protected async confirmTransition(): Promise<void> {
    if (!this.transitionReason().trim()) return;
    const t = this.transition();
    if (!t) return;
    if (t.status === 'Superseded' && this.replacedBy() === null) return;
    const saved = await this.store.transitionBrokerStop(t.stop.id, {
      status: t.status,
      replacedByRecordId: t.status === 'Superseded' ? this.replacedBy() : null,
      reason: this.transitionReason().trim() || null,
      rowVersion: t.stop.rowVersion,
    });
    if (saved) {
      this.transition.set(null);
      this.savedMessage.set('Status recorded as reported by you. It is not verified with the broker.');
    }
  }

  protected reload(): void {
    this.hydratedVersion = undefined;
    this.store.loadPosition({ account: this.data.account, symbol: this.data.symbol });
  }

  protected close(): void {
    this.store.clearPosition();
    this.ref.close();
  }
}
