import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
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

  // ── Email recipients ─────────────────────────────────────────────────────
  protected readonly recipientEmails = signal<string[]>([]);
  protected readonly emailInputValue = signal('');
  protected readonly savingEmails = signal(false);
  protected readonly loadingEmails = signal(true);
  protected readonly sendingTestEmail = signal(false);
  protected readonly scanningNow = signal(false);

  ngOnInit(): void {
    const cfg = this.configService.config();
    this.form.setValue({
      scanIntervalSeconds: cfg.scanIntervalSeconds,
      portfolioRefreshSeconds: cfg.portfolioRefreshSeconds,
      watchlistRefreshSeconds: cfg.watchlistRefreshSeconds,
      rsiOversoldThreshold: cfg.rsiOversoldThreshold,
      rsiOverboughtThreshold: cfg.rsiOverboughtThreshold,
    });

    // Load recipients from backend
    this.notificationApi.getRecipients().subscribe({
      next: (r) => {
        this.recipientEmails.set(r.emails ?? []);
        this.loadingEmails.set(false);
      },
      error: () => this.loadingEmails.set(false),
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
}
