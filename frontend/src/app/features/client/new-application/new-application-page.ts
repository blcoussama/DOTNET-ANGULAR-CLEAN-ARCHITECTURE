import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { DecimalPipe, Location, PercentPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Injectable,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatStepper, MatStepperIntl, MatStepperModule } from '@angular/material/stepper';
import { Router, RouterLink } from '@angular/router';
import { Observable, forkJoin, map, of, startWith } from 'rxjs';
import { ApplicationsApi } from '../../../core/api/applications.api';
import { ProductsApi } from '../../../core/api/products.api';
import { ProfileApi } from '../../../core/api/profile.api';
import { applyServerErrors, errorMessage, toApiError } from '../../../core/http/problem-details';
import { PRODUCT_DESCRIPTIONS, PRODUCT_ICONS, PRODUCT_LABELS } from '../../../core/i18n/labels';
import {
  ApplicationDetail,
  ApplicationDocument,
  ApplicationDraft,
  ClientProfile,
  CreditApplication,
  CreditProduct,
  PRODUCT_TYPES,
  ProductType,
  SimulationRequest,
} from '../../../core/models/api.models';
import { NotificationService } from '../../../core/notify/notification.service';
import { defaultLoanValues, exceedsDebtThreshold, isWithinBounds } from '../../../core/util/loan';
import { LoanFields } from '../../../shared/components/loan-fields';
import { SimulationSummary } from '../../../shared/components/simulation-summary';
import { ErrorState, LoadingState } from '../../../shared/components/states';
import { LabelPipe } from '../../../shared/pipes/label.pipe';
import { MadPipe } from '../../../shared/pipes/mad.pipe';
import { liveSimulation } from '../../../shared/services/live-simulation';
import { isProfileComplete } from '../profile/profile-form';
import { DocumentUploader, missingRequiredDocuments } from '../shared/document-uploader';

@Injectable()
export class FrenchStepperIntl extends MatStepperIntl {
  override optionalLabel = 'Facultatif';
  override completedLabel = 'Terminé';
  override editableLabel = 'Modifiable';
}

const EDITABLE_STATUSES = ['DRAFT', 'INFO_REQUESTED'];

interface PageData {
  products: CreditProduct[];
  profile: ClientProfile;
  application: ApplicationDetail | null;
}

/**
 * Nouvelle demande en 4 étapes : produit → montant & durée (simulation en direct, alerte
 * d'endettement) → pièces → récapitulatif & soumission. Le brouillon est créé à la fin de l'étape 2
 * (les pièces se rattachent à une demande existante) ; la même page sert à reprendre un brouillon.
 */
@Component({
  selector: 'app-new-application-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DecimalPipe,
    PercentPipe,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatRadioModule,
    MatStepperModule,
    LoanFields,
    SimulationSummary,
    LoadingState,
    ErrorState,
    DocumentUploader,
    LabelPipe,
    MadPipe,
  ],
  providers: [{ provide: MatStepperIntl, useClass: FrenchStepperIntl }],
  templateUrl: './new-application-page.html',
  styleUrl: './new-application-page.scss',
})
export class NewApplicationPage implements OnInit {
  private readonly productsApi = inject(ProductsApi);
  private readonly profileApi = inject(ProfileApi);
  private readonly applicationsApi = inject(ApplicationsApi);
  private readonly notify = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly fb = inject(NonNullableFormBuilder);

  /** Paramètre de route (reprise d'un brouillon). */
  readonly id = input<string | undefined>();
  /** Pré-remplissage depuis le simulateur public (`?produit=AUTO&montant=…&duree=…`). */
  readonly produit = input<string | undefined>();
  readonly montant = input<string | undefined>();
  readonly duree = input<string | undefined>();

  private readonly stepper = viewChild(MatStepper);

  protected readonly labels = PRODUCT_LABELS;
  protected readonly descriptions = PRODUCT_DESCRIPTIONS;
  protected readonly icons = PRODUCT_ICONS;

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe([Breakpoints.XSmall, Breakpoints.Small])
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly notEditable = signal<CreditApplication | null>(null);
  protected readonly products = signal<CreditProduct[]>([]);
  protected readonly profile = signal<ClientProfile | null>(null);
  protected readonly draft = signal<CreditApplication | null>(null);
  protected readonly documents = signal<ApplicationDocument[]>([]);
  protected readonly savingDraft = signal(false);
  protected readonly submitting = signal(false);
  protected readonly stepError = signal<string | null>(null);
  /** Valeurs du brouillon telles qu'enregistrées côté serveur (détecte les modifications). */
  private readonly savedSnapshot = signal<string | null>(null);

  protected readonly productForm = this.fb.group({
    productType: this.fb.control<ProductType | null>(null, [Validators.required]),
  });
  protected readonly loanForm = this.fb.group({
    amount: this.fb.control(0, [Validators.required]),
    durationMonths: this.fb.control(0, [Validators.required]),
    purpose: this.fb.control('', [
      Validators.required,
      Validators.minLength(5),
      Validators.maxLength(500),
    ]),
  });
  protected readonly confirmForm = this.fb.group({
    certify: this.fb.control(false, [Validators.requiredTrue]),
  });

  private readonly productType = toSignal(this.productForm.controls.productType.valueChanges, {
    initialValue: null,
  });
  private readonly loanValue = toSignal(
    this.loanForm.valueChanges.pipe(
      startWith(null),
      map(() => this.loanForm.getRawValue()),
    ),
    { requireSync: true },
  );

  protected readonly selectedProduct = computed(
    () => this.products().find((p) => p.type === this.productType()) ?? null,
  );
  protected readonly profileComplete = computed(() => isProfileComplete(this.profile()));
  protected readonly missingDocuments = computed(() => missingRequiredDocuments(this.documents()));
  protected readonly draftSynced = computed(() => {
    const snapshot = this.savedSnapshot();
    return (
      this.draft() !== null &&
      snapshot !== null &&
      snapshot === this.snapshotOf(this.productType(), this.loanValue())
    );
  });
  protected readonly isInfoRequested = computed(() => this.draft()?.status === 'INFO_REQUESTED');

  protected readonly simulationRequest = computed<SimulationRequest | null>(() => {
    const product = this.selectedProduct();
    const { amount, durationMonths } = this.loanValue();
    if (!product || !isWithinBounds(product, Number(amount), Number(durationMonths))) return null;
    const request: SimulationRequest = {
      productType: product.type,
      amount: Number(amount),
      durationMonths: Number(durationMonths),
    };
    const profile = this.profile();
    if (profile?.monthlyIncome && profile.monthlyIncome > 0) {
      request.monthlyIncome = profile.monthlyIncome;
      request.monthlyCharges = profile.monthlyCharges ?? 0;
    }
    return request;
  });
  protected readonly simulation = liveSimulation(this.simulationRequest);
  protected readonly overThreshold = computed(() => exceedsDebtThreshold(this.simulation().result));
  protected readonly profileLink = computed(() => {
    const draft = this.draft();
    return draft ? `/client/demandes/${draft.id}/modifier` : '/client/demandes/nouvelle';
  });

  constructor() {
    this.productForm.controls.productType.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((type) => this.applyProductBounds(type));
  }

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    const id = this.id();
    const application$: Observable<ApplicationDetail | null> = id
      ? this.applicationsApi.get(id)
      : of(null);
    forkJoin({
      products: this.productsApi.listActive(),
      profile: this.profileApi.getMine(),
      application: application$,
    }).subscribe({
      next: (data) => this.init(data),
      error: (error: unknown) => {
        this.loadError.set(errorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private init({ products, profile, application }: PageData): void {
    this.products.set(products);
    this.profile.set(profile);
    if (application) {
      if (!EDITABLE_STATUSES.includes(application.status)) {
        this.notEditable.set(application);
        this.loading.set(false);
        return;
      }
      this.draft.set(application);
      this.documents.set(application.documents);
      this.productForm.setValue({ productType: application.productType });
      this.loanForm.setValue({
        amount: application.amount,
        durationMonths: application.durationMonths,
        purpose: application.purpose ?? '',
      });
      this.savedSnapshot.set(this.snapshotOf(application.productType, this.loanForm.getRawValue()));
    } else {
      const requested = PRODUCT_TYPES.find((t) => t === this.produit()?.toUpperCase());
      const product = products.find((p) => p.type === requested);
      if (product) {
        this.productForm.setValue({ productType: product.type });
        const amount = Number(this.montant());
        const duration = Number(this.duree());
        if (isWithinBounds(product, amount, duration)) {
          this.loanForm.patchValue({ amount, durationMonths: duration });
        }
      }
    }
    this.loading.set(false);
  }

  private applyProductBounds(type: ProductType | null): void {
    const product = this.products().find((p) => p.type === type);
    if (!product) return;
    const { amount, durationMonths } = this.loanForm.controls;
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
    const current = this.loanForm.getRawValue();
    const defaults = defaultLoanValues(product);
    this.loanForm.patchValue({
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
    this.loanForm.updateValueAndValidity();
  }

  private snapshotOf(
    type: ProductType | null,
    loan: { amount: number; durationMonths: number; purpose: string },
  ): string {
    return JSON.stringify([
      type,
      Number(loan.amount),
      Number(loan.durationMonths),
      loan.purpose.trim(),
    ]);
  }

  /** Étape 2 → 3 : crée ou met à jour le brouillon, puis avance. */
  protected saveDraftAndContinue(): void {
    this.loanForm.markAllAsTouched();
    const type = this.productType();
    if (this.loanForm.invalid || !type || this.savingDraft()) return;
    if (this.draftSynced()) {
      this.goToStep(2);
      return;
    }
    const { amount, durationMonths, purpose } = this.loanForm.getRawValue();
    const body: ApplicationDraft = {
      productType: type,
      amount: Number(amount),
      durationMonths: Number(durationMonths),
      purpose: purpose.trim(),
    };
    const existing = this.draft();
    this.savingDraft.set(true);
    this.stepError.set(null);
    const request$ = existing
      ? this.applicationsApi.update(existing.id, body)
      : this.applicationsApi.create(body);
    request$.subscribe({
      next: (saved) => {
        this.savingDraft.set(false);
        this.draft.set({ ...(existing ?? {}), ...saved });
        this.savedSnapshot.set(this.snapshotOf(type, this.loanForm.getRawValue()));
        if (!existing) {
          // L'URL reflète le brouillon créé : un rafraîchissement de la page le reprend.
          this.location.replaceState(`/client/demandes/${saved.id}/modifier`);
          this.notify.success(`Brouillon ${saved.reference} enregistré.`);
        }
        this.goToStep(2);
      },
      error: (error: unknown) => {
        this.savingDraft.set(false);
        const apiError = toApiError(error);
        const unmatched = applyServerErrors(this.loanForm, apiError);
        this.stepError.set(unmatched.length ? unmatched.join(' ') : apiError.message);
      },
    });
  }

  /**
   * Avance après l'enregistrement du brouillon : l'étape 2 est marquée complétée immédiatement
   * (sans attendre la détection de changements) pour que le stepper linéaire autorise le passage.
   */
  private goToStep(index: number): void {
    const stepper = this.stepper();
    if (!stepper) return;
    const draftStep = stepper.steps.get(1);
    if (draftStep) draftStep.completed = true;
    stepper.selectedIndex = index;
  }

  protected onUploaded(doc: ApplicationDocument): void {
    this.documents.update((docs) => [...docs, doc]);
  }

  protected onRemoved(doc: ApplicationDocument): void {
    this.documents.update((docs) => docs.filter((d) => d.id !== doc.id));
  }

  protected submit(): void {
    const draft = this.draft();
    this.confirmForm.markAllAsTouched();
    if (!draft || this.submitting() || this.confirmForm.invalid) return;
    if (!this.profileComplete() || this.missingDocuments().length) return;
    this.submitting.set(true);
    this.stepError.set(null);
    this.applicationsApi.submit(draft.id).subscribe({
      next: (submitted) => {
        this.submitting.set(false);
        this.notify.success(
          `Demande ${submitted.reference ?? draft.reference} soumise. Un analyste va l'étudier.`,
        );
        void this.router.navigate(['/client/demandes', draft.id]);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.stepError.set(errorMessage(error));
        this.notify.error(error);
      },
    });
  }
}
