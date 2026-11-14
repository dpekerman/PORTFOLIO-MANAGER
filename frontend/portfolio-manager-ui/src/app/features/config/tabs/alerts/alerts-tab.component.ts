import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NotificationApiService } from '../../../../core/services/notification-api.service';
import {
  MAX_RECIPIENTS,
  NotificationRecipientsStore,
} from '../../../../core/store/notification-recipients.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { ConfigSaveService } from '../../config-save.service';

@Component({
  selector: 'app-alerts-tab',
  templateUrl: './alerts-tab.component.html',
  styleUrl: './alerts-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule,
    SettingsSectionComponent,
  ],
})
export class AlertsTabComponent implements OnInit {
  private readonly store = inject(NotificationRecipientsStore);
  private readonly notificationApi = inject(NotificationApiService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly maxRecipients = MAX_RECIPIENTS;
  protected readonly recipientEmails = this.store.emails;
  protected readonly loadingEmails = this.store.loading;
  protected readonly emailInputValue = signal('');
  protected readonly savingEmails = signal(false);
  protected readonly sendingTestEmail = signal(false);
  protected readonly scanningNow = signal(false);

  constructor() {
    inject(ConfigSaveService).register(
      { id: 'Alert recipients', dirty: this.store.dirty, save: () => this.store.save() },
      inject(DestroyRef),
    );
  }

  ngOnInit(): void {
    this.store.load();
  }

  protected addEmail(value: string): void {
    this.store.add(value);
    // Keep the text while it is not yet a plausible address so the user can finish typing
    if (!value.trim() || value.includes('@')) this.emailInputValue.set('');
  }

  protected removeEmail(email: string): void {
    this.store.remove(email);
  }

  protected onEmailKeydown(event: KeyboardEvent): void {
    const val = this.emailInputValue();
    if ((event.key === 'Enter' || event.key === ',') && val.trim()) {
      event.preventDefault();
      this.addEmail(val);
    }
    if (event.key === 'Backspace' && !val) {
      this.store.removeLast();
    }
  }

  protected sendTestEmail(): void {
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

  protected scanAndNotifyNow(): void {
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

  protected saveEmailRecipients(): void {
    this.savingEmails.set(true);
    this.store.save().subscribe({
      next: (r) => {
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
