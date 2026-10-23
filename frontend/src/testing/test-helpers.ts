import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import localeFr from '@angular/common/locales/fr';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DEFAULT_CURRENCY_CODE, EnvironmentProviders, LOCALE_ID, Provider } from '@angular/core';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { Routes, provideRouter, withComponentInputBinding } from '@angular/router';
import { authInterceptor } from '../app/core/auth/auth.interceptor';
import { AuthStore } from '../app/core/auth/auth.store';
import { AuthResponse, AuthUser, Role } from '../app/core/models/api.models';
import { SIMULATION_DEBOUNCE_MS } from '../app/shared/services/live-simulation';

registerLocaleData(localeFr, 'fr-FR');

/** Fournisseurs communs : HttpClient + intercepteur réel, backend de test, routeur, locale fr-FR. */
export function provideTestEnvironment(routes: Routes = []): (Provider | EnvironmentProviders)[] {
  return [
    provideHttpClient(withInterceptors([authInterceptor])),
    provideHttpClientTesting(),
    provideRouter(routes, withComponentInputBinding()),
    { provide: LOCALE_ID, useValue: 'fr-FR' },
    { provide: DEFAULT_CURRENCY_CODE, useValue: 'MAD' },
    { provide: SIMULATION_DEBOUNCE_MS, useValue: 0 },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline' } },
    { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
  ];
}

export const USERS: Record<Role, AuthUser> = {
  Client: {
    id: 'u-client',
    email: 'client@test.ma',
    firstName: 'Salma',
    lastName: 'Bennani',
    role: 'Client',
  },
  Analyst: {
    id: 'u-analyst',
    email: 'analyste@test.ma',
    firstName: 'Youssef',
    lastName: 'Alaoui',
    role: 'Analyst',
  },
  Admin: {
    id: 'u-admin',
    email: 'admin@test.ma',
    firstName: 'Nadia',
    lastName: 'Tazi',
    role: 'Admin',
  },
};

export function authResponse(role: Role, token = `token-${role}`): AuthResponse {
  return { accessToken: token, expiresIn: 900, user: USERS[role] };
}

/** Ouvre une session via le vrai flux `POST /auth/login` (backend simulé). */
export function signIn(role: Role, token = `token-${role}`): AuthStore {
  const store = TestBed.inject(AuthStore);
  const http = TestBed.inject(HttpTestingController);
  store.login({ email: USERS[role].email, password: 'x' }).subscribe();
  http.expectOne('/api/v1/auth/login').flush(authResponse(role, token));
  return store;
}

/** Laisse s'écouler les timers à 0 ms (debounce, effets) puis rafraîchit la vue. */
export async function settle<T>(fixture?: ComponentFixture<T>, rounds = 2): Promise<void> {
  for (let i = 0; i < rounds; i++) {
    fixture?.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture?.detectChanges();
}

export function text(fixture: ComponentFixture<unknown>): string {
  return (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ') ?? '';
}

export function query<E extends Element = HTMLElement>(
  fixture: ComponentFixture<unknown>,
  selector: string,
): E | null {
  return (fixture.nativeElement as HTMLElement).querySelector<E>(selector);
}

export function testId<E extends Element = HTMLElement>(
  fixture: ComponentFixture<unknown>,
  id: string,
): E | null {
  return query<E>(fixture, `[data-testid="${id}"]`);
}

export function makeFile(
  name: string,
  type: string,
  bytes: number[] | Uint8Array,
  size?: number,
): File {
  const content = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  const file = new File([content as BlobPart], name, { type });
  if (size !== undefined) Object.defineProperty(file, 'size', { value: size });
  return file;
}

export const PDF_BYTES = [0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x37, 0x0a];
export const PNG_BYTES = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00];

/** Élément rendu dans l'overlay CDK (dialogues, menus, snackbars). */
export function overlay<E extends Element = HTMLElement>(selector: string): E | null {
  return document.querySelector<E>(`.cdk-overlay-container ${selector}`);
}

export function typeInto(
  input: HTMLInputElement | HTMLTextAreaElement | null,
  value: string,
): void {
  if (!input) throw new Error('Champ introuvable');
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.dispatchEvent(new Event('blur'));
}

export function click(element: Element | null): void {
  if (!element) throw new Error('Élément introuvable');
  (element as HTMLElement).click();
}
