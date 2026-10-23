import { DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { map, startWith } from 'rxjs';
import { ProductsApi } from '../../../core/api/products.api';
import { AuthStore } from '../../../core/auth/auth.store';
import { errorMessage } from '../../../core/http/problem-details';
import { PRODUCT_DESCRIPTIONS, PRODUCT_ICONS, PRODUCT_LABELS } from '../../../core/i18n/labels';
import { CreditProduct, ProductType, SimulationRequest } from '../../../core/models/api.models';
import { defaultLoanValues, isWithinBounds } from '../../../core/util/loan';
import { LoanFields } from '../../../shared/components/loan-fields';
import { SimulationSummary } from '../../../shared/components/simulation-summary';
import { ErrorState, LoadingState } from '../../../shared/components/states';
import { MadPipe } from '../../../shared/pipes/mad.pipe';
import { liveSimulation } from '../../../shared/services/live-simulation';

@Component({
  selector: 'app-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    LoanFields,
    SimulationSummary,
    LoadingState,
    ErrorState,
    MadPipe,
  ],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss',
})
export class HomePage {
  private readonly productsApi = inject(ProductsApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly labels = PRODUCT_LABELS;
  protected readonly descriptions = PRODUCT_DESCRIPTIONS;
  protected readonly icons = PRODUCT_ICONS;
  protected readonly errorMessage = errorMessage;

  protected readonly products = rxResource({ stream: () => this.productsApi.listActive() });
  protected readonly selectedType = signal<ProductType | null>(null);
  protected readonly selectedProduct = computed<CreditProduct | null>(() => {
    if (!this.products.hasValue()) return null;
    const list = this.products.value() ?? [];
    return list.find((p) => p.type === this.selectedType()) ?? list[0] ?? null;
  });

  protected readonly loan = this.fb.group({
    amount: this.fb.control(50_000, [Validators.required]),
    durationMonths: this.fb.control(36, [Validators.required]),
  });
  protected readonly budget = this.fb.group({
    monthlyIncome: this.fb.control<number | null>(null, [Validators.min(1)]),
    monthlyCharges: this.fb.control<number | null>(null, [Validators.min(0)]),
  });

  private readonly loanValue = toSignal(
    this.loan.valueChanges.pipe(
      startWith(null),
      map(() => this.loan.getRawValue()),
    ),
    {
      requireSync: true,
    },
  );
  private readonly budgetValue = toSignal(
    this.budget.valueChanges.pipe(
      startWith(null),
      map(() => this.budget.getRawValue()),
    ),
    { requireSync: true },
  );

  protected readonly request = computed<SimulationRequest | null>(() => {
    const product = this.selectedProduct();
    const { amount, durationMonths } = this.loanValue();
    if (!product || !isWithinBounds(product, Number(amount), Number(durationMonths))) return null;
    const { monthlyIncome, monthlyCharges } = this.budgetValue();
    const request: SimulationRequest = {
      productType: product.type,
      amount: Number(amount),
      durationMonths: Number(durationMonths),
    };
    if (monthlyIncome !== null && monthlyIncome > 0) {
      request.monthlyIncome = Number(monthlyIncome);
      request.monthlyCharges =
        monthlyCharges !== null && monthlyCharges >= 0 ? Number(monthlyCharges) : 0;
    }
    return request;
  });
  protected readonly simulation = liveSimulation(this.request);

  constructor() {
    // À chaque changement de produit : bornes des validateurs + valeurs ramenées dans la plage.
    effect(() => {
      const product = this.selectedProduct();
      if (!product) return;
      const { amount, durationMonths } = this.loan.controls;
      amount.setValidators([
        Validators.required,
        Validators.min(product.minAmount),
        Validators.max(product.maxAmount),
      ]);
      durationMonths.setValidators([
        Validators.required,
        Validators.min(product.minDurationMonths),
        Validators.max(product.maxDurationMonths),
        Validators.pattern(/^\d+$/),
      ]);
      const current = this.loan.getRawValue();
      const defaults = defaultLoanValues(product);
      this.loan.setValue({
        amount:
          current.amount >= product.minAmount && current.amount <= product.maxAmount
            ? current.amount
            : defaults.amount,
        durationMonths:
          current.durationMonths >= product.minDurationMonths &&
          current.durationMonths <= product.maxDurationMonths
            ? current.durationMonths
            : defaults.durationMonths,
      });
    });
  }

  protected selectProduct(type: ProductType): void {
    this.selectedType.set(type);
  }

  protected apply(): void {
    const product = this.selectedProduct();
    const { amount, durationMonths } = this.loan.getRawValue();
    const queryParams = product
      ? { produit: product.type, montant: amount, duree: durationMonths }
      : {};
    if (this.auth.hasRole('Client')) {
      void this.router.navigate(['/client/demandes/nouvelle'], { queryParams });
    } else if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/inscription'], {
        queryParams: {
          returnUrl: this.router.serializeUrl(
            this.router.createUrlTree(['/client/demandes/nouvelle'], { queryParams }),
          ),
        },
      });
    }
  }
}
