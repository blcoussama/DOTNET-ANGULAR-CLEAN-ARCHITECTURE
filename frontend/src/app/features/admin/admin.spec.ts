import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PRODUCTS } from '../../../testing/fixtures';
import {
  USERS,
  click,
  provideTestEnvironment,
  query,
  settle,
  signIn,
  testId,
  text,
  typeInto,
} from '../../../testing/test-helpers';
import { AdminUser } from '../../core/models/api.models';
import { ProductEditor } from './products/product-editor';
import { generateTemporaryPassword } from './users/create-user-dialog';
import { UsersPage } from './users/users-page';

const isUsers = (r: { url: string }) => r.url === '/api/v1/admin/users';
const USERS_PAGE = {
  items: [
    { ...USERS.Admin, isActive: true },
    { ...USERS.Analyst, isActive: true },
    { ...USERS.Client, isActive: true },
  ] as AdminUser[],
  page: 1,
  pageSize: 20,
  totalCount: 3,
  totalPages: 1,
};

describe('Administration', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [UsersPage, ProductEditor],
      providers: provideTestEnvironment(),
    });
    http = TestBed.inject(HttpTestingController);
    signIn('Admin');
  });

  afterEach(() => {
    http.verify();
    document.querySelector('.cdk-overlay-container')?.replaceChildren();
  });

  it('génère un mot de passe temporaire conforme à la politique', () => {
    const password = generateTemporaryPassword();
    expect(password).toHaveLength(16);
    expect(password).toMatch(/[A-Z]/);
    expect(password).toMatch(/[a-z]/);
    expect(password).toMatch(/\d/);
    expect(password).toMatch(/[^A-Za-z0-9]/);
    expect(generateTemporaryPassword()).not.toBe(password);
  });

  describe('UsersPage', () => {
    let fixture: ComponentFixture<UsersPage>;

    beforeEach(async () => {
      fixture = TestBed.createComponent(UsersPage);
      await settle(fixture);
      http.expectOne(isUsers).flush(USERS_PAGE);
      await settle(fixture);
    });

    it('liste les utilisateurs et protège le compte de l’admin connecté', () => {
      expect(fixture.nativeElement.querySelectorAll('tr.mat-mdc-row').length).toBe(3);
      expect(text(fixture)).toContain('Vous');
      const toggles = fixture.nativeElement.querySelectorAll(
        'mat-slide-toggle button',
      ) as NodeListOf<HTMLButtonElement>;
      expect(toggles[0].disabled).toBe(true);
      expect(toggles[1].disabled).toBe(false);
    });

    it('désactive un compte après confirmation (PATCH isActive)', async () => {
      const toggles = fixture.nativeElement.querySelectorAll('mat-slide-toggle button');
      click(toggles[1]);
      await settle(fixture);
      expect(document.querySelector('.cdk-overlay-container')?.textContent).toContain(
        'ses sessions seront révoquées',
      );
      click(
        [...document.querySelectorAll('.cdk-overlay-container button')].find((b) =>
          b.textContent?.includes('Désactiver'),
        ) ?? null,
      );
      await settle(fixture);
      const req = http.expectOne('/api/v1/admin/users/u-analyst');
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ isActive: false });
      req.flush({ ...USERS.Analyst, isActive: false });
      await settle(fixture);
      http.expectOne(isUsers).flush(USERS_PAGE);
    });

    it('crée un compte analyste depuis la boîte de dialogue', async () => {
      click(testId(fixture, 'create-user'));
      await settle(fixture);
      const field = (name: string) =>
        document.querySelector<HTMLInputElement>(
          `.cdk-overlay-container input[formcontrolname="${name}"]`,
        );
      typeInto(field('firstName'), 'Karim');
      typeInto(field('lastName'), 'Amrani');
      typeInto(field('email'), 'k.amrani@banque.ma');
      click(document.querySelector('.cdk-overlay-container [data-testid="create-user-submit"]'));
      await settle(fixture);
      const req = http.expectOne((r) => r.url === '/api/v1/admin/users' && r.method === 'POST');
      expect(req.request.body).toMatchObject({
        firstName: 'Karim',
        lastName: 'Amrani',
        email: 'k.amrani@banque.ma',
        role: 'Analyst',
      });
      expect(req.request.body.temporaryPassword).toHaveLength(16);
      req.flush({
        id: 'u-new',
        email: 'k.amrani@banque.ma',
        firstName: 'Karim',
        lastName: 'Amrani',
        role: 'Analyst',
        isActive: true,
      });
      await settle(fixture);
      http.expectOne(isUsers).flush(USERS_PAGE);
    });
  });

  describe('ProductEditor', () => {
    let fixture: ComponentFixture<ProductEditor>;

    beforeEach(async () => {
      fixture = TestBed.createComponent(ProductEditor);
      fixture.componentRef.setInput('product', PRODUCTS[0]);
      await settle(fixture);
    });

    it('refuse des bornes incohérentes', async () => {
      typeInto(query(fixture, 'input[formcontrolname="minAmount"]') as HTMLInputElement, '400000');
      query<HTMLFormElement>(fixture, 'form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);
      expect(text(fixture)).toContain('Le montant minimum doit être inférieur ou égal au maximum.');
      http.expectNone('/api/v1/admin/products/CONSOMMATION');
    });

    it('enregistre le nouveau taux en PUT et émet le produit mis à jour', async () => {
      const saved: number[] = [];
      fixture.componentInstance.saved.subscribe((p) => saved.push(p.annualRate));
      typeInto(query(fixture, 'input[formcontrolname="annualRate"]') as HTMLInputElement, '6.9');
      await settle(fixture);
      click(testId(fixture, 'save-CONSOMMATION'));
      await settle(fixture);
      const req = http.expectOne('/api/v1/admin/products/CONSOMMATION');
      expect(req.request.body).toEqual({
        annualRate: 6.9,
        minAmount: 5000,
        maxAmount: 300000,
        minDurationMonths: 6,
        maxDurationMonths: 84,
        isActive: true,
      });
      req.flush({ ...PRODUCTS[0], annualRate: 6.9 });
      await settle(fixture);
      expect(saved).toEqual([6.9]);
    });
  });
});
