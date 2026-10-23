import {
  AbstractControl,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import {
  ClientProfile,
  ClientProfileUpdate,
  EmploymentType,
} from '../../../core/models/api.models';
import { ageAt } from '../../../core/util/loan';

export type ProfileForm = FormGroup<{
  dateOfBirth: FormControl<string>;
  phone: FormControl<string>;
  city: FormControl<string>;
  employmentType: FormControl<EmploymentType | null>;
  employer: FormControl<string>;
  monthlyIncome: FormControl<number | null>;
  monthlyCharges: FormControl<number | null>;
  yearsAtCurrentJob: FormControl<number | null>;
}>;

export const PHONE_PATTERN = /^\+?[0-9][0-9 .-]{7,18}[0-9]$/;
export const MIN_AGE = 18;
export const MAX_AGE = 100;

export function adultAgeValidator(control: AbstractControl<string>): ValidationErrors | null {
  if (!control.value) return null;
  const age = ageAt(control.value);
  if (age === null) return { date: true };
  if (age < MIN_AGE) return { minAge: MIN_AGE };
  if (age > MAX_AGE) return { maxAge: MAX_AGE };
  return null;
}

export function positiveValidator(
  control: AbstractControl<number | null>,
): ValidationErrors | null {
  const value = control.value;
  return value === null || value === undefined || (value as unknown) === '' || Number(value) > 0
    ? null
    : { positive: true };
}

/** Formulaire typé du profil, règles alignées sur la SPEC (revenu > 0, charges ≥ 0, ancienneté ≥ 0). */
export function buildProfileForm(fb: NonNullableFormBuilder): ProfileForm {
  return fb.group({
    dateOfBirth: fb.control('', [Validators.required, adultAgeValidator]),
    phone: fb.control('', [Validators.required, Validators.pattern(PHONE_PATTERN)]),
    city: fb.control('', [Validators.required, Validators.maxLength(100)]),
    employmentType: fb.control<EmploymentType | null>(null, [Validators.required]),
    employer: fb.control('', [Validators.maxLength(150)]),
    monthlyIncome: fb.control<number | null>(null, [
      Validators.required,
      positiveValidator,
      Validators.max(10_000_000),
    ]),
    monthlyCharges: fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0),
      Validators.max(10_000_000),
    ]),
    yearsAtCurrentJob: fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0),
      Validators.max(60),
      Validators.pattern(/^\d+$/),
    ]),
  });
}

export function profileToFormValue(profile: ClientProfile): ReturnType<ProfileForm['getRawValue']> {
  return {
    dateOfBirth: profile.dateOfBirth ?? '',
    phone: profile.phone ?? '',
    city: profile.city ?? '',
    employmentType: profile.employmentType ?? null,
    employer: profile.employer ?? '',
    monthlyIncome: profile.monthlyIncome ?? null,
    monthlyCharges: profile.monthlyCharges ?? null,
    yearsAtCurrentJob: profile.yearsAtCurrentJob ?? null,
  };
}

export function formValueToUpdate(
  value: ReturnType<ProfileForm['getRawValue']>,
): ClientProfileUpdate {
  return {
    dateOfBirth: value.dateOfBirth,
    phone: value.phone.trim(),
    city: value.city.trim(),
    employmentType: value.employmentType as EmploymentType,
    employer: value.employer.trim() || null,
    monthlyIncome: Number(value.monthlyIncome),
    monthlyCharges: Number(value.monthlyCharges),
    yearsAtCurrentJob: Number(value.yearsAtCurrentJob),
  };
}

/** Champs obligatoires pour un profil complet (indicateur de progression). */
export const REQUIRED_PROFILE_FIELDS = [
  'dateOfBirth',
  'phone',
  'city',
  'employmentType',
  'monthlyIncome',
  'monthlyCharges',
  'yearsAtCurrentJob',
] as const;

export function isProfileComplete(profile: ClientProfile | null | undefined): boolean {
  if (!profile) return false;
  if (typeof profile.isComplete === 'boolean') return profile.isComplete;
  return REQUIRED_PROFILE_FIELDS.every(
    (field) => profile[field] !== null && profile[field] !== undefined && profile[field] !== '',
  );
}
