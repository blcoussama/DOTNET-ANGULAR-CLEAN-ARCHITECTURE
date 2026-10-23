import { PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { map, startWith } from 'rxjs';
import { ProfileApi } from '../../../core/api/profile.api';
import { applyServerErrors, errorMessage, toApiError } from '../../../core/http/problem-details';
import { EMPLOYMENT_LABELS } from '../../../core/i18n/labels';
import { EMPLOYMENT_TYPES } from '../../../core/models/api.models';
import { NotificationService } from '../../../core/notify/notification.service';
import { sanitizeReturnUrl } from '../../../core/util/safe-redirect';
import { ErrorState, LoadingState } from '../../../shared/components/states';
import {
  MAX_AGE,
  MIN_AGE,
  REQUIRED_PROFILE_FIELDS,
  buildProfileForm,
  formValueToUpdate,
  isProfileComplete,
  profileToFormValue,
} from './profile-form';

@Component({
  selector: 'app-profile-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    PercentPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    LoadingState,
    ErrorState,
  ],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
})
export class ProfilePage {
  private readonly api = inject(ProfileApi);
  private readonly notify = inject(NotificationService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string | undefined>();

  protected readonly employmentTypes = EMPLOYMENT_TYPES;
  protected readonly employmentLabels = EMPLOYMENT_LABELS;
  protected readonly minAge = MIN_AGE;
  protected readonly maxAge = MAX_AGE;
  protected readonly form = buildProfileForm(inject(NonNullableFormBuilder));

  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly serverErrors = signal<string[]>([]);
  protected readonly serverComplete = signal(false);

  private readonly formValue = toSignal(
    this.form.valueChanges.pipe(
      startWith(null),
      map(() => this.form.getRawValue()),
    ),
    {
      requireSync: true,
    },
  );

  /** Pourcentage de champs obligatoires valides (indicateur « profil complet »). */
  protected readonly completion = computed(() => {
    this.formValue();
    const valid = REQUIRED_PROFILE_FIELDS.filter((field) => {
      const control = this.form.controls[field];
      return control.value !== null && control.value !== '' && control.valid;
    }).length;
    return Math.round((valid / REQUIRED_PROFILE_FIELDS.length) * 100);
  });

  protected readonly currentDebtRatio = computed(() => {
    const { monthlyIncome, monthlyCharges } = this.formValue();
    if (!monthlyIncome || monthlyIncome <= 0 || monthlyCharges === null || monthlyCharges < 0)
      return null;
    return monthlyCharges / monthlyIncome;
  });

  protected readonly safeReturnUrl = computed(() => {
    const url = sanitizeReturnUrl(this.returnUrl());
    return url && url.startsWith('/client/') ? url : null;
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api.getMine().subscribe({
      next: (profile) => {
        this.form.reset(profileToFormValue(profile));
        this.serverComplete.set(isProfileComplete(profile));
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loadError.set(errorMessage(error));
        this.loading.set(false);
      },
    });
  }

  protected goBack(): void {
    const back = this.safeReturnUrl();
    if (back) void this.router.navigateByUrl(back);
  }

  protected save(): void {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.notify.error('Vérifiez les champs signalés en rouge.');
      return;
    }
    this.saving.set(true);
    this.serverErrors.set([]);
    this.api.updateMine(formValueToUpdate(this.form.getRawValue())).subscribe({
      next: (profile) => {
        this.saving.set(false);
        this.serverComplete.set(isProfileComplete(profile));
        this.form.markAsPristine();
        this.notify.success('Profil financier enregistré.');
        const back = this.safeReturnUrl();
        if (back) void this.router.navigateByUrl(back);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        const apiError = toApiError(error);
        const unmatched = applyServerErrors(this.form, apiError);
        this.serverErrors.set(unmatched);
        this.notify.error(apiError.message);
      },
    });
  }
}
