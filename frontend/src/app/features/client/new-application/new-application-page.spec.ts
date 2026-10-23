import { Location } from '@angular/common';
import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatStepper } from '@angular/material/stepper';
import { By } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { COMPLETE_PROFILE, PRODUCTS, application, doc } from '../../../../testing/fixtures';
import {
  click,
  provideTestEnvironment,
  query,
  settle,
  signIn,
  testId,
  text,
  typeInto,
} from '../../../../testing/test-helpers';
import { NewApplicationPage } from './new-application-page';

describe('NewApplicationPage — stepper de demande', () => {
  let fixture: ComponentFixture<NewApplicationPage>;
  let http: HttpTestingController;

  const stepper = () =>
    fixture.debugElement.query(By.directive(MatStepper)).componentInstance as MatStepper;
  const button = (label: string) =>
    [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === label && b.offsetParent !== undefined,
    ) as HTMLButtonElement;

  async function create(
    inputs: Record<string, string> = {},
    profile: object | null = COMPLETE_PROFILE,
    app = application(),
  ): Promise<void> {
    fixture = TestBed.createComponent(NewApplicationPage);
    for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
    await settle(fixture);
    http.expectOne('/api/v1/products').flush(PRODUCTS);
    const profileReq = http.expectOne('/api/v1/clients/me/profile');
    if (profile) profileReq.flush(profile);
    else profileReq.flush(null, { status: 404, statusText: 'Not Found' });
    if (inputs['id']) http.expectOne(`/api/v1/applications/${inputs['id']}`).flush(app);
    await settle(fixture);
  }

  /** Répond aux simulations en direct déclenchées par l'étape 2. */
  function answerSimulations(
    result = {
      annualRate: 7.5,
      monthlyPayment: 1934.4,
      totalCost: 12851.2,
      debtRatio: 0.262,
      withinDebtThreshold: true,
    },
  ): void {
    for (const req of http.match('/api/v1/simulations')) req.flush(result);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [NewApplicationPage],
      providers: provideTestEnvironment(),
    });
    http = TestBed.inject(HttpTestingController);
    signIn('Client');
  });

  afterEach(() => http.verify());

  it('prévient si le profil financier est incomplet', async () => {
    await create({}, null);
    expect(testId(fixture, 'profile-warning')?.textContent).toContain(
      'profil financier est incomplet',
    );
    expect(stepper().selectedIndex).toBe(0);
  });

  it('pré-remplit produit, montant et durée depuis le simulateur', async () => {
    await create({ produit: 'AUTO', montant: '150000', duree: '60' });
    const page = fixture.componentInstance;
    expect(page['productForm'].controls.productType.value).toBe('AUTO');
    expect(page['loanForm'].controls.amount.value).toBe(150000);
    expect(page['loanForm'].controls.durationMonths.value).toBe(60);
    answerSimulations();
  });

  it('ne passe pas l’étape 1 sans produit choisi', async () => {
    await create();
    click(button('Continuer'));
    await settle(fixture);
    expect(stepper().selectedIndex).toBe(0);
    expect(text(fixture)).toContain('Choisissez un type de crédit.');
  });

  it('crée le brouillon à la fin de l’étape 2 puis passe aux pièces', async () => {
    const replaceState = vi.spyOn(TestBed.inject(Location), 'replaceState');
    await create();
    click(query(fixture, '[data-testid="product-CONSOMMATION"] input'));
    await settle(fixture);
    click(button('Continuer'));
    await settle(fixture);
    expect(stepper().selectedIndex).toBe(1);
    answerSimulations();
    await settle(fixture);
    expect(text(fixture)).toContain('Mensualité estimée');

    typeInto(
      query(fixture, 'textarea[formcontrolname="purpose"]') as HTMLTextAreaElement,
      'Achat de mobilier',
    );
    click(testId(fixture, 'save-draft'));
    await settle(fixture);
    const create$ = http.expectOne('/api/v1/applications');
    expect(create$.request.method).toBe('POST');
    expect(create$.request.body).toMatchObject({
      productType: 'CONSOMMATION',
      purpose: 'Achat de mobilier',
    });
    create$.flush(application({ id: 'app-7', reference: 'CF-2026-000007' }));
    await settle(fixture, 3);
    answerSimulations();
    expect(replaceState).toHaveBeenCalledWith('/client/demandes/app-7/modifier');
    expect(stepper().selectedIndex).toBe(2);
    expect(testId(fixture, 'missing-docs')).not.toBeNull();
  });

  it('affiche l’alerte d’endettement au-delà de 40 %', async () => {
    await create({ id: 'app-1' });
    stepper().selectedIndex = 1;
    await settle(fixture);
    answerSimulations({
      annualRate: 7.5,
      monthlyPayment: 6000,
      totalCost: 40000,
      debtRatio: 0.53,
      withinDebtThreshold: false,
    });
    await settle(fixture);
    expect(testId(fixture, 'debt-alert')).not.toBeNull();
  });

  it('interdit la soumission tant que les pièces obligatoires manquent', async () => {
    await create(
      { id: 'app-1' },
      COMPLETE_PROFILE,
      application({ documents: [doc('d1', 'ID_CARD')] }),
    );
    answerSimulations();
    expect(fixture.componentInstance['missingDocuments']()).toEqual(['PAYSLIP']);
    expect(testId<HTMLButtonElement>(fixture, 'submit-application')!.disabled).toBe(true);
  });

  it('soumet la demande complète après certification et redirige vers son détail', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await create(
      { id: 'app-1' },
      COMPLETE_PROFILE,
      application({ documents: [doc('d1', 'ID_CARD'), doc('d2', 'PAYSLIP')] }),
    );
    answerSimulations();
    stepper().selectedIndex = 3;
    await settle(fixture);
    expect(stepper().selectedIndex).toBe(3);
    expect(text(fixture)).toContain('Vérifiez votre demande');

    click(testId(fixture, 'submit-application'));
    await settle(fixture);
    http.expectNone('/api/v1/applications/app-1/submit');
    expect(text(fixture)).toContain('Vous devez cocher cette case');

    click(query(fixture, '[data-testid="certify"] input'));
    click(testId(fixture, 'submit-application'));
    await settle(fixture);
    http.expectOne('/api/v1/applications/app-1/submit').flush(application({ status: 'SUBMITTED' }));
    await settle(fixture);
    expect(navigate).toHaveBeenCalledWith(['/client/demandes', 'app-1']);
  });

  it('refuse de modifier une demande déjà approuvée', async () => {
    await create({ id: 'app-1' }, COMPLETE_PROFILE, application({ status: 'APPROVED' }));
    expect(text(fixture)).toContain('ne peut plus être modifiée');
    expect(fixture.debugElement.query(By.directive(MatStepper))).toBeNull();
  });
});
