import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { PRODUCTS } from '../../../../testing/fixtures';
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
import { formatMad } from '../../../shared/pipes/mad.pipe';
import { HomePage } from './home-page';

describe('HomePage — simulateur public', () => {
  let fixture: ComponentFixture<HomePage>;
  let http: HttpTestingController;

  async function create(): Promise<void> {
    fixture = TestBed.createComponent(HomePage);
    await settle(fixture);
    http.expectOne('/api/v1/products').flush(PRODUCTS);
    await settle(fixture);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HomePage], providers: provideTestEnvironment() });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('affiche les produits et simule en direct avec les valeurs par défaut', async () => {
    await create();
    expect(fixture.nativeElement.querySelectorAll('mat-button-toggle').length).toBe(3);
    const req = http.expectOne('/api/v1/simulations');
    expect(req.request.body).toEqual({
      productType: 'CONSOMMATION',
      amount: 50000,
      durationMonths: 36,
    });
    req.flush({ annualRate: 7.5, monthlyPayment: 1555.31, totalCost: 5991.16 });
    await settle(fixture);
    expect(testId(fixture, 'monthly-payment')?.textContent).toContain(formatMad(1555.31, 2));
    expect(text(fixture)).toContain('7,5');
  });

  it('borne les valeurs au changement de produit (immobilier)', async () => {
    await create();
    http
      .expectOne('/api/v1/simulations')
      .flush({ annualRate: 7.5, monthlyPayment: 1, totalCost: 1 });
    const toggles = fixture.nativeElement.querySelectorAll('mat-button-toggle button');
    click(toggles[2]);
    await settle(fixture);
    const req = http.expectOne('/api/v1/simulations');
    expect(req.request.body.productType).toBe('IMMOBILIER');
    expect(req.request.body.amount).toBeGreaterThanOrEqual(100000);
    expect(req.request.body.durationMonths).toBeGreaterThanOrEqual(60);
    req.flush({ annualRate: 4.9, monthlyPayment: 1, totalCost: 1 });
  });

  it('n’appelle pas l’API et désactive le bouton si le montant est hors bornes', async () => {
    await create();
    http
      .expectOne('/api/v1/simulations')
      .flush({ annualRate: 7.5, monthlyPayment: 1, totalCost: 1 });
    typeInto(query<HTMLInputElement>(fixture, 'input[aria-describedby="sim-amount-hint"]'), '1000');
    await settle(fixture);
    http.expectNone('/api/v1/simulations');
    expect(text(fixture)).toContain('Le montant doit être compris entre');
    const cta = [...fixture.nativeElement.querySelectorAll('button')].find((b: HTMLButtonElement) =>
      b.textContent?.includes('Déposer ma demande'),
    );
    expect((cta as HTMLButtonElement).disabled).toBe(true);
  });

  it('calcule l’endettement avec le revenu saisi et alerte au-delà de 40 %', async () => {
    await create();
    http
      .expectOne('/api/v1/simulations')
      .flush({ annualRate: 7.5, monthlyPayment: 1, totalCost: 1 });
    typeInto(query<HTMLInputElement>(fixture, 'input[formcontrolname="monthlyIncome"]'), '4000');
    typeInto(query<HTMLInputElement>(fixture, 'input[formcontrolname="monthlyCharges"]'), '500');
    await settle(fixture);
    const req = http.expectOne('/api/v1/simulations');
    expect(req.request.body).toMatchObject({ monthlyIncome: 4000, monthlyCharges: 500 });
    req.flush({
      annualRate: 7.5,
      monthlyPayment: 1555.31,
      totalCost: 5991.16,
      debtRatio: 0.51,
      withinDebtThreshold: false,
    });
    await settle(fixture);
    expect(testId(fixture, 'debt-alert')).not.toBeNull();
    expect(testId(fixture, 'debt-ratio')?.textContent).toContain('51');
  });

  it('affiche une erreur de simulation sans bloquer la page', async () => {
    await create();
    http
      .expectOne('/api/v1/simulations')
      .flush(
        { title: 'Bad Request', detail: 'Montant hors bornes.' },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle(fixture);
    expect(text(fixture)).toContain('Montant hors bornes.');
  });

  it('propose l’inscription au visiteur et la demande au client connecté', async () => {
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    await create();
    http
      .expectOne('/api/v1/simulations')
      .flush({ annualRate: 7.5, monthlyPayment: 1, totalCost: 1 });
    const cta = () =>
      [...fixture.nativeElement.querySelectorAll('button')].find((b: HTMLButtonElement) =>
        b.textContent?.includes('Déposer ma demande'),
      );
    click(cta());
    expect(navigate).toHaveBeenCalledWith(['/inscription'], {
      queryParams: {
        returnUrl: '/client/demandes/nouvelle?produit=CONSOMMATION&montant=50000&duree=36',
      },
    });
    signIn('Client');
    click(cta());
    expect(navigate).toHaveBeenLastCalledWith(['/client/demandes/nouvelle'], {
      queryParams: { produit: 'CONSOMMATION', montant: 50000, duree: 36 },
    });
  });

  it('affiche un état d’erreur si les produits sont indisponibles', async () => {
    fixture = TestBed.createComponent(HomePage);
    await settle(fixture);
    http.expectOne('/api/v1/products').flush(null, { status: 503, statusText: 'Unavailable' });
    await settle(fixture);
    expect(text(fixture)).toContain('Les offres de crédit sont indisponibles');
    expect(query(fixture, '[role="alert"]')).not.toBeNull();
  });
});
