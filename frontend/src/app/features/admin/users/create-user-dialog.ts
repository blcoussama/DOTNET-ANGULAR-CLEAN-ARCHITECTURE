import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AdminUsersApi } from '../../../core/api/admin-users.api';
import { applyServerErrors, toApiError } from '../../../core/http/problem-details';
import { AdminUser, StaffRole } from '../../../core/models/api.models';
import { passwordPolicy } from '../../auth/password.validators';

/** Mot de passe temporaire robuste généré via `crypto.getRandomValues` (jamais `Math.random`). */
export function generateTemporaryPassword(length = 16): string {
  const sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789', '!@#$%&*?-_'];
  const all = sets.join('');
  const random = new Uint32Array(length);
  crypto.getRandomValues(random);
  const chars = Array.from(random, (n, i) =>
    i < sets.length ? sets[i][n % sets[i].length] : all[n % all.length],
  );
  // Mélange (Fisher–Yates) pour ne pas laisser les classes en tête.
  const shuffle = new Uint32Array(length);
  crypto.getRandomValues(shuffle);
  for (let i = chars.length - 1; i > 0; i--) {
    const j = shuffle[i] % (i + 1);
    [chars[i], chars[j]] = [chars[j], chars[i]];
  }
  return chars.join('');
}

@Component({
  selector: 'app-create-user-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Créer un compte collaborateur</h2>
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <mat-dialog-content>
        @if (error()) {
          <p class="cf-alert cf-alert-danger" role="alert">{{ error() }}</p>
        }
        <div class="cf-form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Prénom</mat-label>
            <input matInput formControlName="firstName" required cdkFocusInitial />
            @if (form.controls.firstName.invalid) {
              <mat-error>{{
                form.controls.firstName.getError('server') || 'Le prénom est obligatoire.'
              }}</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Nom</mat-label>
            <input matInput formControlName="lastName" required />
            @if (form.controls.lastName.invalid) {
              <mat-error>{{
                form.controls.lastName.getError('server') || 'Le nom est obligatoire.'
              }}</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline" class="cf-full">
            <mat-label>Adresse e-mail professionnelle</mat-label>
            <input matInput type="email" formControlName="email" required />
            @if (form.controls.email.hasError('server')) {
              <mat-error>{{ form.controls.email.getError('server') }}</mat-error>
            } @else if (form.controls.email.invalid) {
              <mat-error>Adresse e-mail invalide.</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline" class="cf-full">
            <mat-label>Rôle</mat-label>
            <mat-select formControlName="role">
              <mat-option value="Analyst">Analyste</mat-option>
              <mat-option value="Admin">Administrateur</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="cf-full">
            <mat-label>Mot de passe temporaire</mat-label>
            <input
              matInput
              [type]="showPassword() ? 'text' : 'password'"
              formControlName="temporaryPassword"
              autocomplete="new-password"
              required
            />
            <button
              mat-icon-button
              matSuffix
              type="button"
              (click)="showPassword.set(!showPassword())"
              [attr.aria-label]="
                showPassword() ? 'Masquer le mot de passe' : 'Afficher le mot de passe'
              "
            >
              <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
            </button>
            <mat-hint>À transmettre par un canal sûr ; à changer à la première connexion.</mat-hint>
            @if (form.controls.temporaryPassword.hasError('passwordPolicy')) {
              <mat-error
                >8 caractères min., avec majuscule, minuscule, chiffre et caractère
                spécial.</mat-error
              >
            } @else if (form.controls.temporaryPassword.invalid) {
              <mat-error>{{
                form.controls.temporaryPassword.getError('server') ||
                  'Le mot de passe est obligatoire.'
              }}</mat-error>
            }
          </mat-form-field>
        </div>
        <button mat-button type="button" (click)="regenerate()">
          <mat-icon aria-hidden="true">autorenew</mat-icon> Générer un autre mot de passe
        </button>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Annuler</button>
        <button
          mat-flat-button
          type="submit"
          [disabled]="saving()"
          data-testid="create-user-submit"
        >
          {{ saving() ? 'Création…' : 'Créer le compte' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
})
export class CreateUserDialog {
  private readonly api = inject(AdminUsersApi);
  private readonly ref = inject<MatDialogRef<CreateUserDialog, AdminUser>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly form = this.fb.group({
    firstName: this.fb.control('', [Validators.required, Validators.maxLength(100)]),
    lastName: this.fb.control('', [Validators.required, Validators.maxLength(100)]),
    email: this.fb.control('', [Validators.required, Validators.email, Validators.maxLength(256)]),
    role: this.fb.control<StaffRole>('Analyst', [Validators.required]),
    temporaryPassword: this.fb.control(generateTemporaryPassword(), [
      Validators.required,
      passwordPolicy,
    ]),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected regenerate(): void {
    this.form.controls.temporaryPassword.setValue(generateTemporaryPassword());
    this.showPassword.set(true);
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    this.api
      .create({
        ...value,
        email: value.email.trim(),
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
      })
      .subscribe({
        next: (user) => this.ref.close(user),
        error: (err: unknown) => {
          this.saving.set(false);
          const apiError = toApiError(err);
          if (apiError.status === 409) {
            this.form.controls.email.setErrors({
              server: 'Cette adresse e-mail est déjà utilisée.',
            });
            return;
          }
          const unmatched = applyServerErrors(this.form, apiError);
          this.error.set(
            unmatched.length
              ? unmatched.join(' ')
              : Object.keys(apiError.fieldErrors).length
                ? null
                : apiError.message,
          );
        },
      });
  }
}
