import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { toApiError } from '../../core/http/problem-details';
import { NotificationService } from '../../core/notify/notification.service';
import { resolvePostLoginUrl, sanitizeReturnUrl } from '../../core/util/safe-redirect';

@Component({
  selector: 'app-login-page',
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
  templateUrl: './login-page.html',
  styleUrl: './auth-pages.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  /** Paramètre de requête `?returnUrl=` (lié par le routeur). */
  readonly returnUrl = input<string | undefined>();

  protected readonly form = this.fb.group({
    email: this.fb.control('', [Validators.required, Validators.email, Validators.maxLength(256)]),
    password: this.fb.control('', [Validators.required]),
  });
  protected readonly submitting = signal(false);
  protected readonly errorText = signal<string | null>(null);
  protected readonly hidePassword = signal(true);

  protected registerQueryParams() {
    const safe = sanitizeReturnUrl(this.returnUrl());
    return safe ? { returnUrl: safe } : {};
  }

  protected submit(): void {
    if (this.submitting()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.submitting.set(true);
    this.errorText.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login({ email: email.trim(), password }).subscribe({
      next: (user) => {
        this.submitting.set(false);
        this.notify.success(`Bienvenue, ${user.firstName} !`);
        void this.router.navigateByUrl(resolvePostLoginUrl(this.returnUrl(), user.role));
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        const apiError = toApiError(error);
        if (apiError.status === 401 || apiError.status === 400) {
          this.errorText.set('Adresse e-mail ou mot de passe incorrect.');
        } else if (apiError.status === 423 || apiError.status === 403) {
          this.errorText.set(apiError.problem?.detail || 'Ce compte est verrouillé ou désactivé.');
        } else {
          this.errorText.set(apiError.message);
        }
        this.form.controls.password.reset();
      },
    });
  }
}
