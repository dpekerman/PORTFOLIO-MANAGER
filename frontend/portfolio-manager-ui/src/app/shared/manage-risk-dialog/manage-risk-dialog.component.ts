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
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
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
  priceBasisLabel,
  summarizeCoverage,
  validateBrokerStop,
  validateRiskPlan,
} from '../../features/swing-trading/swing-trading-logic';

type TransitionStatus = 'UserReportedCancelled' | 'UserReportedFilled' | 'Superseded';

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
  placedDate: string;
  brokerOrderRef: string;
  lastUserVerifiedDate: string;
  notes: string;
  reason: string;
  allowOverCoverage: boolean;
}

const emptyStop = (): StopModel => ({
  orderType: 'StopMarket',
  triggerPrice: null,
  limitPrice: null,
  coveredQuantity: null,
  placedDate: '',
  brokerOrderRef: '',
  lastUserVerifiedDate: '',
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
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
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
  protected readonly priceUsable = computed(
    () => this.data.currentPrice != null && (this.data.priceBasis === 'Live' || this.data.priceBasis === 'EOD'),
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
      currentPrice: this.data.currentPrice ?? null,
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
      currentPrice: this.data.currentPrice ?? null,
      priceUsable: this.priceUsable(),
    }),
  );
  protected readonly stopInvalid = computed(() => hasErrors(this.stopValidation()));

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
  }

  protected basisLabel(): string {
    return priceBasisLabel(this.data.priceBasis ?? 'Unavailable');
  }

  protected statusLabel(s: BrokerStop): string {
    return brokerStopStatusLabel(s);
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
  }

  protected async savePlan(): Promise<void> {
    if (this.planInvalid()) return;
    const m = this.planModel();
    const plan = this.position()?.plan;
    const saved = await this.store.savePlan({
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
    this.savedMessage.set(saved ? 'Risk plan saved. No broker order was changed.' : '');
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
      placedDate: s.placedDate?.substring(0, 10) ?? '',
      brokerOrderRef: s.brokerOrderRef ?? '',
      lastUserVerifiedDate: s.lastUserVerifiedDate?.substring(0, 10) ?? '',
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
      placedDate: m.placedDate || null,
      brokerOrderRef: m.brokerOrderRef.trim() || null,
      lastUserVerifiedDate: m.lastUserVerifiedDate || null,
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
