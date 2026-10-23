import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Politique de mot de passe alignée sur ASP.NET Identity (configuration attendue : 8 caractères min.). */
export const PASSWORD_MIN_LENGTH = 8;

export const passwordPolicy: ValidatorFn = (
  control: AbstractControl<string>,
): ValidationErrors | null => {
  const value = control.value ?? '';
  if (!value) return null;
  const missing: string[] = [];
  if (value.length < PASSWORD_MIN_LENGTH) missing.push(`${PASSWORD_MIN_LENGTH} caractères`);
  if (!/[a-z]/.test(value)) missing.push('une minuscule');
  if (!/[A-Z]/.test(value)) missing.push('une majuscule');
  if (!/\d/.test(value)) missing.push('un chiffre');
  if (!/[^a-zA-Z0-9]/.test(value)) missing.push('un caractère spécial');
  return missing.length ? { passwordPolicy: missing } : null;
};

export function matchFieldValidator(field: string, confirmField: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const a = group.get(field)?.value;
    const b = group.get(confirmField)?.value;
    return a && b && a !== b ? { mismatch: true } : null;
  };
}
