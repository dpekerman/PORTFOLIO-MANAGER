import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ConfigService } from '../../core/services/config.service';

@Component({
  selector: 'app-config-page',
  templateUrl: './config-page.component.html',
  styleUrl: './config-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule,
  ],
})
export class ConfigPageComponent implements OnInit {
  private readonly configService = inject(ConfigService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

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
  });

  ngOnInit(): void {
    // Sync form if config changes externally (e.g. reset in another tab)
    const cfg = this.configService.config();
    this.form.setValue({
      scanIntervalSeconds: cfg.scanIntervalSeconds,
      portfolioRefreshSeconds: cfg.portfolioRefreshSeconds,
      watchlistRefreshSeconds: cfg.watchlistRefreshSeconds,
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.configService.update({
      scanIntervalSeconds: this.form.value.scanIntervalSeconds ?? 300,
      portfolioRefreshSeconds: this.form.value.portfolioRefreshSeconds ?? 120,
      watchlistRefreshSeconds: this.form.value.watchlistRefreshSeconds ?? 60,
    });
    this.snackBar.open('Settings saved and applied immediately.', 'OK', { duration: 3000 });
  }

  reset(): void {
    this.configService.reset();
    const cfg = this.configService.config();
    this.form.setValue({
      scanIntervalSeconds: cfg.scanIntervalSeconds,
      portfolioRefreshSeconds: cfg.portfolioRefreshSeconds,
      watchlistRefreshSeconds: cfg.watchlistRefreshSeconds,
    });
    this.snackBar.open('Settings reset to defaults.', 'OK', { duration: 3000 });
  }
}
