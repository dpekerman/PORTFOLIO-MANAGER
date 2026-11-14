import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AppRole, CreateUserRequest } from '../../core/models/portfolio.models';

@Component({
  selector: 'app-create-user-dialog',
  templateUrl: './create-user-dialog.component.html',
  styleUrl: './create-user-dialog.component.scss',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
})
export class CreateUserDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<CreateUserDialogComponent>);
  private readonly fb = inject(FormBuilder);

  protected readonly form = this.fb.group({
    displayName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(10)]],
    role: ['Viewer' as AppRole, [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) return;
    const { displayName, email, password, role } = this.form.value;
    this.dialogRef.close({ displayName, email, password, role } as CreateUserRequest);
  }
}
