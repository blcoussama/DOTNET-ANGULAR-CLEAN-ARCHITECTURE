import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { startWith } from 'rxjs';
import { AuthStore } from '../../core/auth/auth.store';
import { applyServerErrors, toApiError } from '../../core/http/problem-details';
import { NotificationService } from '../../core/notify/notification.service';
import { sanitizeReturnUrl } from '../../core/util/safe-redirect';
import { PASSWORD_MIN_LENGTH, matchFieldValidator, passwordPolicy } from './password.validators';

const NAME_PATTERN = /^[\p{L}][\p{L}' -]*$/u;

@Component({
  selector: 'app-register-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './register-page.html',
  styleUrl: './auth-pages.scss',
})
export class RegisterPage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly returnUrl = input<string | undefined>();

  protected readonly form = this.fb.group(
    {
      firstName: this.fb.control('', [
        Validators.required,
        Validators.maxLength(100),
        Validators.pattern(NAME_PATTERN),
      ]),
      lastName: this.fb.control('', [
        Validators.required,
        Validators.maxLength(100),
        Validators.pattern(NAME_PATTERN),
      ]),
      email: this.fb.control('', [
        Validators.required,
        Validators.email,
        Validators.maxLength(256),
      ]),
      password: this.fb.control('', [
        Validators.required,
        passwordPolicy,
        Validators.maxLength(128),
      ]),
      confirmPassword: this.fb.control('', [Validators.required]),
    },
    { validators: matchFieldValidator('password', 'confirmPassword') },
  );
  protected readonly submitting = signal(false);
  protected readonly serverErrors = signal<string[]>([]);
  protected readonly hidePassword = signal(true);

  private readonly password = toSignal(
    this.form.controls.password.valueChanges.pipe(startWith('')),
    { initialValue: '' },
  );
  protected readonly rules = computed(() => {
    const value = this.password();
    return [
      {
        label: `${PASSWORD_MIN_LENGTH} caractères minimum`,
        ok: value.length >= PASSWORD_MIN_LENGTH,
      },
      { label: 'Une minuscule et une majuscule', ok: /[a-z]/.test(value) && /[A-Z]/.test(value) },
      { label: 'Un chiffre', ok: /\d/.test(value) },
      { label: 'Un caractère spécial', ok: /[^a-zA-Z0-9]/.test(value) },
    ];
  });

  protected loginQueryParams() {
    const safe = sanitizeReturnUrl(this.returnUrl());
    return safe ? { returnUrl: safe } : {};
  }

  protected submit(): void {
    if (this.submitting()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.submitting.set(true);
    this.serverErrors.set([]);
    const { firstName, lastName, email, password } = this.form.getRawValue();
    this.auth
      .register({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        password,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.notify.success(
            'Compte créé. Complétez votre profil financier pour déposer une demande.',
          );
          const target = sanitizeReturnUrl(this.returnUrl());
          void this.router.navigateByUrl(
            target && target.startsWith('/client/') ? target : '/client/profil',
          );
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          const apiError = toApiError(error);
          if (apiError.status === 409) {
            this.form.controls.email.setErrors({
              server: 'Un compte existe déjà avec cette adresse e-mail.',
            });
            return;
          }
          const unmatched = applyServerErrors(this.form, apiError);
          this.serverErrors.set(
            unmatched.length
              ? unmatched
              : Object.keys(apiError.fieldErrors).length
                ? []
                : [apiError.message],
          );
        },
      });
  }
}
