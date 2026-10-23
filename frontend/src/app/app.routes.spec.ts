import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, TitleStrategy } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PRODUCTS } from '../testing/fixtures';
import { provideTestEnvironment, settle, signIn } from '../testing/test-helpers';
import { routes } from './app.routes';
import { CreditFlowTitleStrategy } from './core/i18n/title-strategy';

describe('Routage (gardes, lazy loading, titres)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ...provideTestEnvironment(routes),
        { provide: TitleStrategy, useClass: CreditFlowTitleStrategy },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  it('redirige un visiteur de l’espace client vers la connexion avec returnUrl', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/client/demandes');
    expect(TestBed.inject(Title).getTitle()).toBe('Connexion · CreditFlow');
    expect(harness.routeNativeElement?.textContent).toContain('Connexion');
    expect(TestBed.inject(Router).url).toBe('/connexion?returnUrl=%2Fclient%2Fdemandes');
  });

  it('interdit l’espace analyste à un client', async () => {
    signIn('Client');
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/analyste/file');
    expect(TestBed.inject(Router).url).toBe('/acces-refuse');
    expect(harness.routeNativeElement?.textContent).toContain('Accès refusé');
  });

  it('renvoie un utilisateur connecté de /connexion vers son espace', async () => {
    signIn('Admin');
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/connexion');
    expect(TestBed.inject(Router).url).toBe('/admin/utilisateurs');
    http
      .match(() => true)
      .forEach((r) => r.flush({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }));
  });

  it('affiche la page 404 pour une route inconnue', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/nulle-part');
    expect(harness.routeNativeElement?.textContent).toContain('Page introuvable');
    expect(TestBed.inject(Title).getTitle()).toBe('Page introuvable · CreditFlow');
  });

  it('sert le simulateur public à la racine', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    await settle(harness.fixture);
    http.expectOne('/api/v1/products').flush(PRODUCTS);
    await settle(harness.fixture);
    http
      .match('/api/v1/simulations')
      .forEach((r) => r.flush({ annualRate: 7.5, monthlyPayment: 1, totalCost: 1 }));
    expect(harness.routeNativeElement?.textContent).toContain('Simulateur de crédit');
    expect(TestBed.inject(Title).getTitle()).toBe('Simulateur de crédit · CreditFlow');
  });
});
