import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';
import { AdminUsersApi } from '../../../core/api/admin-users.api';
import { AuthStore } from '../../../core/auth/auth.store';
import { errorMessage } from '../../../core/http/problem-details';
import { ROLE_LABELS } from '../../../core/i18n/labels';
import {
  AdminUser,
  AdminUsersQuery,
  ROLES,
  Role,
  StaffRole,
  UpdateUserRequest,
} from '../../../core/models/api.models';
import { NotificationService } from '../../../core/notify/notification.service';
import { confirmAction } from '../../../shared/components/confirm-dialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/components/states';
import { LabelPipe } from '../../../shared/pipes/label.pipe';
import { latestValue } from '../../../shared/util/resource-utils';
import { CreateUserDialog } from './create-user-dialog';

@Component({
  selector: 'app-users-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTableModule,
    EmptyState,
    ErrorState,
    LoadingState,
    LabelPipe,
  ],
  templateUrl: './users-page.html',
})
export class UsersPage {
  private readonly api = inject(AdminUsersApi);
  private readonly auth = inject(AuthStore);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);

  protected readonly roles = ROLES;
  protected readonly staffRoles: StaffRole[] = ['Analyst', 'Admin'];
  protected readonly roleLabels = ROLE_LABELS;
  protected readonly columns = ['name', 'email', 'role', 'active', 'createdAt'];
  protected readonly errorMessage = errorMessage;

  protected readonly query = signal<AdminUsersQuery>({
    page: 1,
    pageSize: 20,
    role: null,
    search: null,
  });
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly users = rxResource({
    params: () => this.query(),
    stream: ({ params }) => this.api.list(params),
  });
  protected readonly data = latestValue(this.users);
  protected readonly rows = computed(() => this.data()?.items ?? []);
  protected readonly total = computed(() => this.data()?.totalCount ?? 0);
  protected readonly pendingId = signal<string | null>(null);

  constructor() {
    this.searchControl.valueChanges
      .pipe(
        map((v) => v.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((search) => this.query.update((q) => ({ ...q, search: search || null, page: 1 })));
  }

  protected isSelf(user: AdminUser): boolean {
    return user.id === this.auth.user()?.id;
  }

  protected filterRole(role: Role | null): void {
    this.query.update((q) => ({ ...q, role, page: 1 }));
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected openCreate(): void {
    this.dialog
      .open<CreateUserDialog, void, AdminUser>(CreateUserDialog, {
        width: '560px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((created) => {
        if (!created) return;
        this.notify.success(
          `Compte ${ROLE_LABELS[created.role] ?? ''} créé pour ${created.firstName} ${created.lastName}.`,
        );
        this.users.reload();
      });
  }

  protected changeRole(user: AdminUser, role: Role): void {
    if (role === user.role || this.isSelf(user)) return;
    confirmAction(this.dialog, {
      title: 'Changer le rôle ?',
      message: `${user.firstName} ${user.lastName} deviendra « ${ROLE_LABELS[role]} ».`,
      confirmLabel: 'Changer le rôle',
    }).subscribe((ok) => {
      if (ok) {
        this.update(user, { role }, 'Rôle mis à jour.');
      } else {
        // Restaure l'affichage du select.
        this.resetRows();
      }
    });
  }

  protected toggleActive(user: AdminUser, isActive: boolean): void {
    if (this.isSelf(user)) return;
    const message = isActive
      ? `${user.firstName} ${user.lastName} pourra de nouveau se connecter.`
      : `${user.firstName} ${user.lastName} ne pourra plus se connecter et ses sessions seront révoquées.`;
    confirmAction(this.dialog, {
      title: isActive ? 'Réactiver le compte ?' : 'Désactiver le compte ?',
      message,
      confirmLabel: isActive ? 'Réactiver' : 'Désactiver',
      destructive: !isActive,
    }).subscribe((ok) => {
      if (ok) {
        this.update(user, { isActive }, isActive ? 'Compte réactivé.' : 'Compte désactivé.');
      } else {
        this.resetRows();
      }
    });
  }

  /** Recrée les lignes pour annuler visuellement un changement abandonné (interrupteur, liste). */
  private resetRows(): void {
    this.users.update((page) =>
      page ? { ...page, items: page.items.map((u) => ({ ...u })) } : page,
    );
  }

  private update(user: AdminUser, body: UpdateUserRequest, success: string): void {
    this.pendingId.set(user.id);
    this.api.update(user.id, body).subscribe({
      next: () => {
        this.pendingId.set(null);
        this.notify.success(success);
        this.users.reload();
      },
      error: (error: unknown) => {
        this.pendingId.set(null);
        this.notify.error(error);
        this.users.reload();
      },
    });
  }
}
