import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppRole, CreateUserRequest } from '../../../../core/models/portfolio.models';
import { UsersStore } from '../../../../core/store/users.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { AllocConfirmDialogComponent } from '../../alloc-confirm-dialog.component';
import { CreateUserDialogComponent } from '../../create-user-dialog.component';

@Component({
  selector: 'app-users-tab',
  templateUrl: './users-tab.component.html',
  styleUrl: './users-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    MatTooltipModule,
    SettingsSectionComponent,
  ],
})
export class UsersTabComponent implements OnInit {
  private readonly store = inject(UsersStore);
  private readonly dialog = inject(MatDialog);

  protected readonly userList = this.store.users;
  protected readonly loadingUsers = this.store.loading;
  protected readonly savingUser = this.store.saving;
  protected readonly availableRoles: AppRole[] = ['Admin', 'Trader', 'Viewer'];

  ngOnInit(): void {
    this.store.load();
  }

  protected loadUsers(): void {
    this.store.load();
  }

  protected openCreateUserDialog(): void {
    this.dialog
      .open(CreateUserDialogComponent, { width: '400px' })
      .afterClosed()
      .subscribe((req: CreateUserRequest | undefined) => {
        if (req) this.store.create(req);
      });
  }

  protected changeUserRole(userId: string, role: string): void {
    this.store.changeRole(userId, role as AppRole);
  }

  protected confirmDeleteUser(userId: string, displayName: string): void {
    this.dialog
      .open(AllocConfirmDialogComponent, {
        data: { title: `Delete ${displayName}?`, message: 'This action cannot be undone.' },
        width: '340px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.store.remove(userId);
      });
  }
}
