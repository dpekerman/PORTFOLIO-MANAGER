import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { AuthApiService } from '../../../core/services/auth-api.service';
import { AuthStateService } from '../../../core/services/auth-state.service';

function passwordsMatch(group: import('@angular/forms').AbstractControl) {
  const pw = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return pw === confirm ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-setup',
  templateUrl: './setup.component.html',
  styleUrl: './setup.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
})
export class SetupComponent implements OnInit {
  private readonly authApi = inject(AuthApiService);
  private readonly authState = inject(AuthStateService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly hidePassword = signal(true);

  protected readonly form = this.fb.group(
    {
      displayName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  ngOnInit(): void {
    // Redirect if setup is already done
    if (this.authState.setupRequired() === false) {
      this.router.navigate(['/login']);
    }
  }

  protected submit(): void {
    if (this.form.invalid || this.loading()) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    const { displayName, email, password, confirmPassword } = this.form.value;
    this.authApi
      .completeSetup({
        displayName: displayName!,
        email: email!,
        password: password!,
        confirmPassword: confirmPassword!,
      })
      .subscribe({
        next: (response) => {
          this.authState.setAuth(response);
          this.authState.setupRequired.set(false);
          this.router.navigate(['/portfolio']);
        },
        error: (err) => {
          const msg =
            err?.error?.message ?? err?.error?.errors?.[0] ?? 'Setup failed. Please try again.';
          this.errorMessage.set(msg);
          this.loading.set(false);
        },
      });
  }
}
