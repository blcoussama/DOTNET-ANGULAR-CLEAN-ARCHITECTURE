import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ProductsApi } from '../../../core/api/products.api';
import { applyServerErrors, toApiError } from '../../../core/http/problem-details';
import { PRODUCT_ICONS, PRODUCT_LABELS } from '../../../core/i18n/labels';
import { CreditProduct } from '../../../core/models/api.models';
import { NotificationService } from '../../../core/notify/notification.service';

/** Bornes cohérentes : min ≤ max pour le montant et la durée. */
export function boundsValidator(group: AbstractControl): ValidationErrors | null {
  const v = group.value as Partial<CreditProduct>;
  const errors: ValidationErrors = {};
  if (v.minAmount != null && v.maxAmount != null && Number(v.minAmount) > Number(v.maxAmount))
    errors['amountRange'] = true;
  if (
    v.minDurationMonths != null &&
    v.maxDurationMonths != null &&
    Number(v.minDurationMonths) > Number(v.maxDurationMonths)
  ) {
    errors['durationRange'] = true;
  }
  return Object.keys(errors).length ? errors : null;
}

@Component({
  selector: 'app-product-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSlideToggleModule,
  ],
  templateUrl: './product-editor.html',
})
export class ProductEditor implements OnInit {
  private readonly api = inject(ProductsApi);
  private readonly notify = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly product = input.required<CreditProduct>();
  readonly saved = output<CreditProduct>();

  protected readonly labels = PRODUCT_LABELS;
  protected readonly icons = PRODUCT_ICONS;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.group(
    {
      annualRate: this.fb.control(0, [Validators.required, Validators.min(0), Validators.max(30)]),
      minAmount: this.fb.control(0, [Validators.required, Validators.min(1)]),
      maxAmount: this.fb.control(0, [Validators.required, Validators.min(1)]),
      minDurationMonths: this.fb.control(0, [
        Validators.required,
        Validators.min(1),
        Validators.max(600),
        Validators.pattern(/^\d+$/),
      ]),
      maxDurationMonths: this.fb.control(0, [
        Validators.required,
        Validators.min(1),
        Validators.max(600),
        Validators.pattern(/^\d+$/),
      ]),
      isActive: this.fb.control(true),
    },
    { validators: boundsValidator },
  );

  ngOnInit(): void {
    this.reset();
  }

  protected reset(): void {
    const { type: _type, ...values } = this.product();
    this.form.reset(values);
    this.error.set(null);
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    const v = this.form.getRawValue();
    const body = {
      annualRate: Number(v.annualRate),
      minAmount: Number(v.minAmount),
      maxAmount: Number(v.maxAmount),
      minDurationMonths: Number(v.minDurationMonths),
      maxDurationMonths: Number(v.maxDurationMonths),
      isActive: v.isActive,
    };
    this.api.update(this.product().type, body).subscribe({
      next: (product) => {
        this.saving.set(false);
        const updated = { ...this.product(), ...body, ...(product ?? {}) };
        this.form.reset({ ...body, ...(product ?? {}) });
        this.notify.success(`${PRODUCT_LABELS[updated.type]} mis à jour.`);
        this.saved.emit(updated);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        const apiError = toApiError(err);
        const unmatched = applyServerErrors(this.form, apiError);
        this.error.set(unmatched.length ? unmatched.join(' ') : apiError.message);
      },
    });
  }
}
