import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  authResponse,
  provideTestEnvironment,
  query,
  settle,
  testId,
  text,
  typeInto,
} from '../../../testing/test-helpers';
import { LoginPage } from './login-page';
import { passwordPolicy } from './password.validators';
import { RegisterPage } from './register-page';
import { FormControl } from '@angular/forms';

describe('Pages d’authentification', () => {
  let http: HttpTestingController;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [LoginPage, RegisterPage],
      providers: provideTestEnvironment(),
    });
    http = TestBed.inject(HttpTestingController);
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  describe('LoginPage', () => {
    let fixture: ComponentFixture<LoginPage>;

    async function login(returnUrl?: string): Promise<void> {
      fixture = TestBed.createComponent(LoginPage);
      if (returnUrl) fixture.componentRef.setInput('returnUrl', returnUrl);
      await settle(fixture);
      typeInto(query(fixture, 'input[type="email"]') as HTMLInputElement, 'client@test.ma');
      typeInto(query(fixture, 'input[formcontrolname="password"]') as HTMLInputElement, 'Secret1!');
      query<HTMLFormElement>(fixture, 'form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);
    }

    it('redirige vers le returnUrl interne autorisé', async () => {
      await login('/client/profil');
      http.expectOne('/api/v1/auth/login').flush(authResponse('Client'));
      await settle(fixture);
      expect(navigateByUrl).toHaveBeenCalledWith('/client/profil');
    });

    it('ignore un returnUrl externe (redirection ouverte)', async () => {
      await login('https://evil.example/');
      http.expectOne('/api/v1/auth/login').flush(authResponse('Analyst'));
      await settle(fixture);
      expect(navigateByUrl).toHaveBeenCalledWith('/analyste/file');
    });

    it('affiche un message générique sur identifiants invalides et vide le mot de passe', async () => {
      await login();
      http
        .expectOne('/api/v1/auth/login')
        .flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
      await settle(fixture);
      expect(testId(fixture, 'login-error')?.textContent).toContain(
        'Adresse e-mail ou mot de passe incorrect.',
      );
      expect((query(fixture, 'input[formcontrolname="password"]') as HTMLInputElement).value).toBe(
        '',
      );
    });

    it('signale la limitation de débit (429)', async () => {
      await login();
      http
        .expectOne('/api/v1/auth/login')
        .flush(null, { status: 429, statusText: 'Too Many Requests' });
      await settle(fixture);
      expect(testId(fixture, 'login-error')?.textContent).toContain('Trop de tentatives');
    });

    it('n’envoie rien si le formulaire est invalide', async () => {
      fixture = TestBed.createComponent(LoginPage);
      await settle(fixture);
      query<HTMLFormElement>(fixture, 'form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);
      http.expectNone('/api/v1/auth/login');
      expect(text(fixture)).toContain("L'adresse e-mail est obligatoire.");
    });
  });

  describe('RegisterPage', () => {
    let fixture: ComponentFixture<RegisterPage>;

    async function fill(password = 'Secret12!'): Promise<void> {
      fixture = TestBed.createComponent(RegisterPage);
      await settle(fixture);
      typeInto(query(fixture, 'input[formcontrolname="firstName"]') as HTMLInputElement, 'Salma');
      typeInto(query(fixture, 'input[formcontrolname="lastName"]') as HTMLInputElement, 'Bennani');
      typeInto(
        query(fixture, 'input[formcontrolname="email"]') as HTMLInputElement,
        'salma@test.ma',
      );
      typeInto(query(fixture, 'input[formcontrolname="password"]') as HTMLInputElement, password);
      typeInto(
        query(fixture, 'input[formcontrolname="confirmPassword"]') as HTMLInputElement,
        password,
      );
      query<HTMLFormElement>(fixture, 'form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);
    }

    it('applique la politique de mot de passe côté client', () => {
      expect(passwordPolicy(new FormControl('abc') as never)).toEqual({
        passwordPolicy: ['8 caractères', 'une majuscule', 'un chiffre', 'un caractère spécial'],
      });
      expect(passwordPolicy(new FormControl('Secret12!') as never)).toBeNull();
    });

    it('inscrit, connecte puis oriente vers le profil financier', async () => {
      await fill();
      http.expectOne('/api/v1/auth/register').flush({}, { status: 201, statusText: 'Created' });
      http.expectOne('/api/v1/auth/login').flush(authResponse('Client'));
      await settle(fixture);
      expect(navigateByUrl).toHaveBeenCalledWith('/client/profil');
    });

    it('signale un e-mail déjà utilisé (409) sur le champ', async () => {
      await fill();
      http
        .expectOne('/api/v1/auth/register')
        .flush({ title: 'Conflict' }, { status: 409, statusText: 'Conflict' });
      await settle(fixture);
      expect(testId(fixture, 'email-server-error')?.textContent).toContain('Un compte existe déjà');
    });

    it('affiche les erreurs Identity non rattachées à un champ', async () => {
      await fill();
      http.expectOne('/api/v1/auth/register').flush(
        {
          status: 400,
          errors: {
            PasswordRequiresUniqueChars: ['Le mot de passe doit contenir 4 caractères différents.'],
          },
        },
        { status: 400, statusText: 'Bad Request' },
      );
      await settle(fixture);
      expect(testId(fixture, 'register-errors')?.textContent).toContain('4 caractères différents');
    });
  });
});
