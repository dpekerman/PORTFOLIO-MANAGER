import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, forkJoin, of } from 'rxjs';
import {
  AllocationRiskConfig,
  AllocationRiskTarget,
  AllocationSectorTarget,
  SinglePositionLimit,
} from '../../core/models/portfolio.models';
import { ConfigService } from '../../core/services/config.service';
import { NotificationApiService } from '../../core/services/notification-api.service';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';

@Component({
  selector: 'app-config-page',
  templateUrl: './config-page.component.html',
  styleUrl: './config-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTimepickerModule,
    MatDialogModule,
    MatTooltipModule,
  ],
})
export class ConfigPageComponent implements OnInit {
  private readonly configService = inject(ConfigService);
  private readonly notificationApi = inject(NotificationApiService);
  private readonly api = inject(PortfolioApiService);
  private readonly scannerState = inject(ScannerStateService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  // ── Refresh interval + RSI form ──────────────────────────────────────────
  protected readonly form = this.fb.group({
    scanIntervalSeconds: [
      this.configService.config().scanIntervalSeconds,
      [Validators.required, Validators.min(30), Validators.max(3600)],
    ],
    portfolioRefreshSeconds: [
      this.configService.config().portfolioRefreshSeconds,
      [Validators.required, Validators.min(30), Validators.max(3600)],
    ],
    watchlistRefreshSeconds: [
      this.configService.config().watchlistRefreshSeconds,
      [Validators.required, Validators.min(30), Validators.max(3600)],
    ],
    rsiOversoldThreshold: [
      this.configService.config().rsiOversoldThreshold,
      [Validators.required, Validators.min(1), Validators.max(49)],
    ],
    rsiOverboughtThreshold: [
      this.configService.config().rsiOverboughtThreshold,
      [Validators.required, Validators.min(51), Validators.max(99)],
    ],
  });

  // ── EOD Window form ──────────────────────────────────────────────────────
  // Form controls use Date | null — required by MatTimepickerInput.
  // Use ConfigPageComponent.timeStrToDate / dateToTimeString to convert to/from backend "HH:mm".
  protected readonly eodForm = this.fb.group({
    eodWindowStart: [
      ConfigPageComponent.timeStrToDate(this.configService.config().eodWindowStart),
      [Validators.required],
    ],
    eodWindowEnd: [
      ConfigPageComponent.timeStrToDate(this.configService.config().eodWindowEnd),
      [Validators.required],
    ],
    eodWindowEnabled: [this.configService.config().eodWindowEnabled],
  });

  protected readonly savingEodSettings = signal(false);
  protected readonly eodWindowActive = this.scannerState.eodWindowActive;
  protected readonly isSavingAll = signal(false);

  // ── Value Screener Schedule form ─────────────────────────────────────────
  // Mirrors EOD form approach: Date | null for timepicker, convert to/from HH:mm string.
  protected readonly vsForm = this.fb.group({
    vsScheduleTime: [ConfigPageComponent.timeStrToDate('17:00'), [Validators.required]],
    vsScheduleEnabled: [true],
  });
  protected readonly savingVsSchedule = signal(false);

  // ── Email recipients ─────────────────────────────────────────────────────
  protected readonly recipientEmails = signal<string[]>([]);
  protected readonly emailInputValue = signal('');
  protected readonly savingEmails = signal(false);
  protected readonly loadingEmails = signal(true);
  protected readonly sendingTestEmail = signal(false);
  protected readonly scanningNow = signal(false);

  // ── Sector / Industry Lists ──────────────────────────────────────────────
  protected readonly sectors = signal<string[]>([]);
  protected readonly industries = signal<string[]>([]);
  protected readonly newSectorInput = signal('');
  protected readonly newIndustryInput = signal('');
  protected readonly savingLists = signal(false);
  protected readonly sectorFilter = signal('');
  protected readonly industryFilter = signal('');
  protected readonly filteredSectors = computed(() => {
    const f = this.sectorFilter().toLowerCase();
    return f ? this.sectors().filter((s) => s.toLowerCase().includes(f)) : this.sectors();
  });
  protected readonly filteredIndustries = computed(() => {
    const f = this.industryFilter().toLowerCase();
    return f ? this.industries().filter((i) => i.toLowerCase().includes(f)) : this.industries();
  });

  // ── Allocation & Risk Management ─────────────────────────────────────────
  protected readonly riskTargets = signal<AllocationRiskTarget[]>([]);
  protected readonly sectorTargets = signal<AllocationSectorTarget[]>([]);
  protected readonly positionLimits = signal<SinglePositionLimit[]>([]);
  protected readonly savingAllocation = signal(false);
  protected readonly allocationDirty = signal(false);

  // Pending server-side deletes (IDs removed locally but not yet deleted on server)
  private pendingRiskDeletes: number[] = [];
  private pendingSectorDeletes: number[] = [];
  private pendingLimitDeletes: number[] = [];
  private _tempIdCounter = -1;

  // Edit state for inline editing
  protected readonly editingRisk = signal<{
    id: number | null;
    role: string;
    targetPct: number | null;
  } | null>(null);
  protected readonly editingSector = signal<{
    id: number | null;
    sector: string;
    targetPct: number | null;
  } | null>(null);
  protected readonly editingLimit = signal<{
    id: number | null;
    role: string;
    targetPct: number | null;
  } | null>(null);

  protected readonly riskTotal = computed(() =>
    this.riskTargets().reduce((s, r) => s + r.targetPct, 0),
  );
  protected readonly sectorTotal = computed(() =>
    this.sectorTargets().reduce((s, r) => s + r.targetPct, 0),
  );

  private loadAllocationRisk(): void {
    this.api.getAllocationRiskConfig().subscribe({
      next: (cfg: AllocationRiskConfig) => {
        this.riskTargets.set(cfg.riskTargets);
        this.sectorTargets.set(cfg.sectorTargets);
        this.positionLimits.set(cfg.positionLimits);
      },
    });
  }

  // ── Risk Targets ──────────────────────────────────────────────────────────
  protected startAddRisk(): void {
    this.editingRisk.set({ id: null, role: '', targetPct: null });
  }
  protected startEditRisk(item: AllocationRiskTarget): void {
    this.editingRisk.set({ id: item.id, role: item.role, targetPct: item.targetPct });
  }
  protected cancelEditRisk(): void {
    this.editingRisk.set(null);
  }
  protected saveRisk(): void {
    const e = this.editingRisk();
    if (!e || !e.role.trim() || !e.targetPct) return;
    if (e.id === null) {
      this.riskTargets.update((items) => [
        ...items,
        {
          id: this._tempIdCounter--,
          role: e.role.trim(),
          targetPct: e.targetPct!,
          displayOrder: items.length,
        },
      ]);
    } else {
      this.riskTargets.update((items) =>
        items.map((i) =>
          i.id === e.id ? { ...i, role: e.role.trim(), targetPct: e.targetPct! } : i,
        ),
      );
    }
    this.editingRisk.set(null);
    this.allocationDirty.set(true);
  }
  protected deleteRisk(id: number): void {
    this.dialog
      .open(AllocConfirmDialogComponent, {
        data: {
          title: 'Delete entry?',
          message: 'This entry will be removed when you save changes.',
        },
        width: '340px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (!confirmed) return;
        if (id > 0) this.pendingRiskDeletes.push(id);
        this.riskTargets.update((items) => items.filter((i) => i.id !== id));
        this.allocationDirty.set(true);
      });
  }

  // ── Sector Targets ────────────────────────────────────────────────────────
  protected startAddSector(): void {
    this.editingSector.set({ id: null, sector: '', targetPct: null });
  }
  protected startEditSector(item: AllocationSectorTarget): void {
    this.editingSector.set({ id: item.id, sector: item.sector, targetPct: item.targetPct });
  }
  protected cancelEditSector(): void {
    this.editingSector.set(null);
  }
  protected saveSector(): void {
    const e = this.editingSector();
    if (!e || !e.sector.trim() || !e.targetPct) return;
    if (e.id === null) {
      this.sectorTargets.update((items) => [
        ...items,
        {
          id: this._tempIdCounter--,
          sector: e.sector.trim(),
          targetPct: e.targetPct!,
          displayOrder: items.length,
        },
      ]);
    } else {
      this.sectorTargets.update((items) =>
        items.map((i) =>
          i.id === e.id ? { ...i, sector: e.sector.trim(), targetPct: e.targetPct! } : i,
        ),
      );
    }
    this.editingSector.set(null);
    this.allocationDirty.set(true);
  }
  protected deleteSector(id: number): void {
    this.dialog
      .open(AllocConfirmDialogComponent, {
        data: {
          title: 'Delete entry?',
          message: 'This entry will be removed when you save changes.',
        },
        width: '340px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (!confirmed) return;
        if (id > 0) this.pendingSectorDeletes.push(id);
        this.sectorTargets.update((items) => items.filter((i) => i.id !== id));
        this.allocationDirty.set(true);
      });
  }

  // ── Position Limits ───────────────────────────────────────────────────────
  protected startAddLimit(): void {
    this.editingLimit.set({ id: null, role: '', targetPct: null });
  }
  protected startEditLimit(item: SinglePositionLimit): void {
    this.editingLimit.set({ id: item.id, role: item.role, targetPct: item.targetPct });
  }
  protected cancelEditLimit(): void {
    this.editingLimit.set(null);
  }
  protected saveLimit(): void {
    const e = this.editingLimit();
    if (!e || !e.role.trim() || !e.targetPct) return;
    if (e.id === null) {
      this.positionLimits.update((items) => [
        ...items,
        {
          id: this._tempIdCounter--,
          role: e.role.trim(),
          targetPct: e.targetPct!,
          displayOrder: items.length,
        },
      ]);
    } else {
      this.positionLimits.update((items) =>
        items.map((i) =>
          i.id === e.id ? { ...i, role: e.role.trim(), targetPct: e.targetPct! } : i,
        ),
      );
    }
    this.editingLimit.set(null);
    this.allocationDirty.set(true);
  }
  protected deleteLimit(id: number): void {
    this.dialog
      .open(AllocConfirmDialogComponent, {
        data: {
          title: 'Delete entry?',
          message: 'This entry will be removed when you save changes.',
        },
        width: '340px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (!confirmed) return;
        if (id > 0) this.pendingLimitDeletes.push(id);
        this.positionLimits.update((items) => items.filter((i) => i.id !== id));
        this.allocationDirty.set(true);
      });
  }

  protected saveAllocationRisk(): void {
    this.savingAllocation.set(true);
    const calls: Observable<unknown>[] = [
      ...this.pendingRiskDeletes.map((id) => this.api.deleteRiskTarget(id)),
      ...this.pendingSectorDeletes.map((id) => this.api.deleteSectorTarget(id)),
      ...this.pendingLimitDeletes.map((id) => this.api.deletePositionLimit(id)),
      ...this.riskTargets().map((r) =>
        this.api.upsertRiskTarget(r.id > 0 ? r.id : null, r.role, r.targetPct),
      ),
      ...this.sectorTargets().map((s) =>
        this.api.upsertSectorTarget(s.id > 0 ? s.id : null, s.sector, s.targetPct),
      ),
      ...this.positionLimits().map((l) =>
        this.api.upsertPositionLimit(l.id > 0 ? l.id : null, l.role, l.targetPct),
      ),
    ];
    (calls.length ? forkJoin(calls) : of([])).subscribe({
      next: () => {
        this.pendingRiskDeletes = [];
        this.pendingSectorDeletes = [];
        this.pendingLimitDeletes = [];
        this.allocationDirty.set(false);
        this.savingAllocation.set(false);
        this.loadAllocationRisk();
        this.snackBar.open('Allocation & Risk settings saved.', 'OK', { duration: 3000 });
      },
      error: () => {
        this.savingAllocation.set(false);
        this.snackBar.open('Failed to save allocation settings.', 'Dismiss', { duration: 4000 });
      },
    });
  }

  ngOnInit(): void {
    const cfg = this.configService.config();
    this.form.setValue({
      scanIntervalSeconds: cfg.scanIntervalSeconds,
      portfolioRefreshSeconds: cfg.portfolioRefreshSeconds,
      watchlistRefreshSeconds: cfg.watchlistRefreshSeconds,
      rsiOversoldThreshold: cfg.rsiOversoldThreshold,
      rsiOverboughtThreshold: cfg.rsiOverboughtThreshold,
    });

    // Load EOD window settings from backend (to show current server-side state)
    this.api.getEodSettings().subscribe({
      next: (s) => {
        this.eodForm.setValue({
          eodWindowStart: ConfigPageComponent.timeStrToDate(s.eodWindowStart),
          eodWindowEnd: ConfigPageComponent.timeStrToDate(s.eodWindowEnd),
          eodWindowEnabled: s.eodWindowEnabled,
        });
        this.configService.update({
          eodWindowStart: s.eodWindowStart,
          eodWindowEnd: s.eodWindowEnd,
          eodWindowEnabled: s.eodWindowEnabled,
        });
      },
      error: () => {}, // Non-critical — keep form defaults
    });

    // Load recipients from backend
    this.notificationApi.getRecipients().subscribe({
      next: (r) => {
        this.recipientEmails.set(r.emails ?? []);
        this.loadingEmails.set(false);
      },
      error: () => this.loadingEmails.set(false),
    });

    // Load sector/industry lists from backend
    this.api.getSectorIndustryLists().subscribe({
      next: (lists) => {
        this.sectors.set(lists.sectors);
        this.industries.set(lists.industries);
      },
      error: () => {
        this.snackBar.open('Could not load sector/industry lists from backend.', 'Dismiss', {
          duration: 4000,
        });
      },
    });

    // Load allocation & risk config
    this.loadAllocationRisk();

    // Load Value Screener schedule
    this.api.getValueScreenerSchedule().subscribe({
      next: (s) => {
        this.vsForm.setValue({
          vsScheduleTime: ConfigPageComponent.timeStrToDate(s.scheduledTimeEt ?? '17:00'),
          vsScheduleEnabled: s.enabled ?? true,
        });
        this.vsForm.markAsPristine();
      },
      error: () => {},
    });
  }

  // ── EOD Window settings ──────────────────────────────────────────────────
  // Static so it can be called from field initializers (before instance methods are accessible).
  static timeStrToDate(timeStr: string): Date | null {
    if (!timeStr) return null;
    const parts = timeStr.split(':').map(Number);
    if (parts.length < 2 || isNaN(parts[0]) || isNaN(parts[1])) return null;
    const d = new Date();
    d.setHours(parts[0], parts[1], 0, 0);
    return d;
  }

  private dateToTimeString(date: Date | null | undefined): string {
    if (!date) return '00:00';
    return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}`;
  }

  saveEodSettings(): void {
    if (this.eodForm.invalid) return;
    const start = this.dateToTimeString(this.eodForm.value.eodWindowStart);
    const end = this.dateToTimeString(this.eodForm.value.eodWindowEnd);
    const enabled = this.eodForm.value.eodWindowEnabled ?? true;

    this.savingEodSettings.set(true);
    this.api
      .updateEodSettings({ eodWindowStart: start, eodWindowEnd: end, eodWindowEnabled: enabled })
      .subscribe({
        next: () => {
          this.configService.update({
            eodWindowStart: start,
            eodWindowEnd: end,
            eodWindowEnabled: enabled,
          });
          this.savingEodSettings.set(false);
          this.eodForm.markAsPristine();
          this.snackBar.open(
            `EOD window saved: ${start}–${end} ET (${enabled ? 'Enabled' : 'Disabled'}).`,
            'OK',
            { duration: 4000 },
          );
        },
        error: () => {
          this.savingEodSettings.set(false);
          this.snackBar.open('Failed to save EOD settings to server.', 'Dismiss', {
            duration: 4000,
          });
        },
      });
  }

  // ── Value Screener Schedule ───────────────────────────────────────────────
  saveVsSchedule(): void {
    if (this.vsForm.invalid) return;
    const timeEt = this.dateToTimeString(this.vsForm.value.vsScheduleTime);
    const enabled = this.vsForm.value.vsScheduleEnabled ?? true;
    this.savingVsSchedule.set(true);
    this.api.updateValueScreenerSchedule(timeEt, enabled).subscribe({
      next: () => {
        this.savingVsSchedule.set(false);
        this.vsForm.markAsPristine();
        this.snackBar.open(
          `Value Screener schedule saved: ${timeEt} ET, ${enabled ? 'Enabled' : 'Disabled'}.`,
          'OK',
          { duration: 4000 },
        );
      },
      error: () => {
        this.savingVsSchedule.set(false);
        this.snackBar.open('Failed to save Value Screener schedule.', 'Dismiss', {
          duration: 4000,
        });
      },
    });
  }

  // ── Interval / RSI settings ──────────────────────────────────────────────
  save(): void {
    if (this.form.invalid) return;
    this.configService.update({
      scanIntervalSeconds: this.form.value.scanIntervalSeconds ?? 300,
      portfolioRefreshSeconds: this.form.value.portfolioRefreshSeconds ?? 120,
      watchlistRefreshSeconds: this.form.value.watchlistRefreshSeconds ?? 60,
      rsiOversoldThreshold: this.form.value.rsiOversoldThreshold ?? 30,
      rsiOverboughtThreshold: this.form.value.rsiOverboughtThreshold ?? 75,
    });
    // Clear server RSI cache so next scan uses the new thresholds
    this.api.clearRsiCache().subscribe({
      complete: () => this.scannerState.refresh(true),
      error: () => this.scannerState.refresh(true), // still refresh even if clear fails
    });
    this.snackBar.open('Settings saved. RSI Scanner will refresh with new thresholds.', 'OK', {
      duration: 4000,
    });
  }

  reset(): void {
    this.configService.reset();
    const cfg = this.configService.config();
    this.form.setValue({
      scanIntervalSeconds: cfg.scanIntervalSeconds,
      portfolioRefreshSeconds: cfg.portfolioRefreshSeconds,
      watchlistRefreshSeconds: cfg.watchlistRefreshSeconds,
      rsiOversoldThreshold: cfg.rsiOversoldThreshold,
      rsiOverboughtThreshold: cfg.rsiOverboughtThreshold,
    });
    this.eodForm.setValue({
      eodWindowStart: ConfigPageComponent.timeStrToDate(cfg.eodWindowStart),
      eodWindowEnd: ConfigPageComponent.timeStrToDate(cfg.eodWindowEnd),
      eodWindowEnabled: cfg.eodWindowEnabled,
    });
    this.snackBar.open('Settings reset to defaults.', 'OK', { duration: 3000 });
  }

  // ── Email recipient management ───────────────────────────────────────────
  addEmail(value: string): void {
    const email = value.trim().toLowerCase();
    if (!email || !email.includes('@')) return;
    if (this.recipientEmails().includes(email)) {
      this.emailInputValue.set('');
      return;
    }
    if (this.recipientEmails().length >= 50) return;
    this.recipientEmails.update((list) => [...list, email]);
    this.emailInputValue.set('');
  }

  removeEmail(email: string): void {
    this.recipientEmails.update((list) => list.filter((e) => e !== email));
  }

  onEmailKeydown(event: KeyboardEvent): void {
    const val = this.emailInputValue();
    if ((event.key === 'Enter' || event.key === ',') && val.trim()) {
      event.preventDefault();
      this.addEmail(val);
    }
    if (event.key === 'Backspace' && !val && this.recipientEmails().length > 0) {
      this.recipientEmails.update((list) => list.slice(0, -1));
    }
  }

  sendTestEmail(): void {
    const addr = this.emailInputValue().trim() || this.recipientEmails()[0];
    if (!addr) {
      this.snackBar.open('Enter an email address to send the test to.', 'OK', { duration: 3000 });
      return;
    }
    this.sendingTestEmail.set(true);
    this.notificationApi.sendTestEmail(addr).subscribe({
      next: (r) => {
        this.sendingTestEmail.set(false);
        if (r.success) {
          this.snackBar.open(`✅ Test email delivered to ${addr}. Check your inbox!`, 'OK', {
            duration: 5000,
          });
        } else {
          this.snackBar.open(`❌ ${r.error}`, 'Dismiss', { duration: 8000 });
        }
      },
      error: (err) => {
        this.sendingTestEmail.set(false);
        const msg = err?.error?.error ?? err?.message ?? 'Unknown error';
        this.snackBar.open(`❌ SMTP error: ${msg}`, 'Dismiss', { duration: 10000 });
      },
    });
  }

  scanAndNotifyNow(): void {
    if (this.recipientEmails().length === 0) {
      this.snackBar.open('Save at least one recipient first.', 'OK', { duration: 3000 });
      return;
    }
    this.scanningNow.set(true);
    this.notificationApi.scanAndNotifyNow().subscribe({
      next: (r) => {
        this.scanningNow.set(false);
        const msg =
          r.message ?? (r.triggered ? 'Scan complete.' : (r.reason ?? 'No signals found.'));
        this.snackBar.open(`📡 ${msg}`, 'OK', { duration: 6000 });
      },
      error: (err) => {
        this.scanningNow.set(false);
        const msg = err?.error?.error ?? err?.message ?? 'Scan failed';
        this.snackBar.open(`❌ ${msg}`, 'Dismiss', { duration: 8000 });
      },
    });
  }

  saveEmailRecipients(): void {
    this.savingEmails.set(true);
    this.notificationApi.updateRecipients(this.recipientEmails()).subscribe({
      next: (r) => {
        this.recipientEmails.set(r.emails ?? []);
        this.savingEmails.set(false);
        this.snackBar.open(
          `${r.emails.length} recipient(s) saved. Alerts will be sent for new CONFIRMED signals.`,
          'OK',
          { duration: 4000 },
        );
      },
      error: () => {
        this.savingEmails.set(false);
        this.snackBar.open('Failed to save recipients. Is the backend running?', 'Dismiss', {
          duration: 5000,
        });
      },
    });
  }

  // ── Sector / Industry list management ────────────────────────────────────
  addSector(value: string): void {
    const v = value.trim();
    if (!v || this.sectors().includes(v)) {
      this.newSectorInput.set('');
      return;
    }
    this.sectors.update((list) => [...list, v].sort());
    this.newSectorInput.set('');
  }

  removeSector(s: string): void {
    this.sectors.update((list) => list.filter((x) => x !== s));
  }

  addIndustry(value: string): void {
    const v = value.trim();
    if (!v || this.industries().includes(v)) {
      this.newIndustryInput.set('');
      return;
    }
    this.industries.update((list) => [...list, v].sort());
    this.newIndustryInput.set('');
  }

  removeIndustry(i: string): void {
    this.industries.update((list) => list.filter((x) => x !== i));
  }

  saveSectorIndustryLists(): void {
    this.savingLists.set(true);
    this.api
      .saveSectorIndustryLists({ sectors: this.sectors(), industries: this.industries() })
      .subscribe({
        next: (lists) => {
          this.sectors.set(lists.sectors);
          this.industries.set(lists.industries);
          this.savingLists.set(false);
          this.snackBar.open('Sector & Industry lists saved.', 'OK', { duration: 3000 });
        },
        error: () => {
          this.savingLists.set(false);
          this.snackBar.open('Failed to save lists.', 'Dismiss', { duration: 4000 });
        },
      });
  }

  /**
   * Saves ALL configuration sections in one action:
   * 1. Scanner intervals + RSI thresholds (browser storage)
   * 2. EOD window settings (backend)
   * 3. Email recipients (backend)
   * 4. Sector & industry lists (backend)
   * Allocation & Risk changes are staged in memory — use the dedicated Save button in that section.
   */
  saveAll(): void {
    if (this.isSavingAll()) return;
    this.isSavingAll.set(true);

    // 1. Scanner settings (synchronous config service)
    if (this.form.valid) {
      this.configService.update({
        scanIntervalSeconds: this.form.value.scanIntervalSeconds ?? 300,
        portfolioRefreshSeconds: this.form.value.portfolioRefreshSeconds ?? 120,
        watchlistRefreshSeconds: this.form.value.watchlistRefreshSeconds ?? 60,
        rsiOversoldThreshold: this.form.value.rsiOversoldThreshold ?? 30,
        rsiOverboughtThreshold: this.form.value.rsiOverboughtThreshold ?? 75,
      });
      this.form.markAsPristine();
      this.api
        .clearRsiCache()
        .subscribe({ complete: () => this.scannerState.refresh(true), error: () => {} });
    }

    // 2. EOD window (async backend)
    if (this.eodForm.valid && !this.eodForm.pristine) {
      const start = this.dateToTimeString(this.eodForm.value.eodWindowStart);
      const end = this.dateToTimeString(this.eodForm.value.eodWindowEnd);
      const enabled = this.eodForm.value.eodWindowEnabled ?? true;
      this.api
        .updateEodSettings({ eodWindowStart: start, eodWindowEnd: end, eodWindowEnabled: enabled })
        .subscribe({
          next: () => {
            this.configService.update({
              eodWindowStart: start,
              eodWindowEnd: end,
              eodWindowEnabled: enabled,
            });
            this.eodForm.markAsPristine();
          },
          error: () => {},
        });
    }

    // 3. Email recipients (async backend)
    this.notificationApi.updateRecipients(this.recipientEmails()).subscribe({ error: () => {} });

    // 4. Sector & industry lists (async backend)
    this.api
      .saveSectorIndustryLists({ sectors: this.sectors(), industries: this.industries() })
      .subscribe({
        next: (lists) => {
          this.sectors.set(lists.sectors);
          this.industries.set(lists.industries);
        },
        error: () => {},
      });

    // Brief visual feedback, then clear the saving state
    setTimeout(() => {
      this.isSavingAll.set(false);
      this.snackBar.open('All configuration settings saved.', 'OK', { duration: 4000 });
    }, 800);
  }

  resetAll(): void {
    this.reset(); // resets scanner form + EOD form to defaults
  }
}

@Component({
  selector: 'app-alloc-confirm-dialog',
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>{{ data.message }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button [mat-dialog-close]="false">Cancel</button>
      <button mat-flat-button color="warn" [mat-dialog-close]="true">
        <mat-icon>delete</mat-icon> Delete
      </button>
    </mat-dialog-actions>
  `,
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
})
export class AllocConfirmDialogComponent {
  readonly data = inject<{ title: string; message: string }>(MAT_DIALOG_DATA);
}
