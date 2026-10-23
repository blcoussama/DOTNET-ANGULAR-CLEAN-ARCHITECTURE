import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { USERS, authResponse, provideTestEnvironment, signIn } from '../../../testing/test-helpers';
import { AuthStore } from './auth.store';

describe('AuthStore', () => {
  let http: HttpTestingController;
  let store: AuthStore;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideTestEnvironment() });
    http = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  it('démarre dans un état inconnu, sans jeton', () => {
    expect(store.status()).toBe('unknown');
    expect(store.isAuthenticated()).toBe(false);
    expect(store.accessToken()).toBeNull();
  });

  it('ouvre la session à la connexion et expose rôle et nom', () => {
    signIn('Analyst', 'jwt-1');
    expect(store.isAuthenticated()).toBe(true);
    expect(store.accessToken()).toBe('jwt-1');
    expect(store.role()).toBe('Analyst');
    expect(store.displayName()).toBe('Youssef Alaoui');
    expect(store.initials()).toBe('YA');
    expect(store.hasRole('Analyst', 'Admin')).toBe(true);
    expect(store.hasRole('Client')).toBe(false);
  });

  it('ne persiste jamais le jeton dans le stockage du navigateur', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    signIn('Client');
    expect(setItem).not.toHaveBeenCalled();
    // Lecture volontaire, uniquement pour vérifier que rien n'y a été écrit.
    // eslint-disable-next-line no-restricted-properties, no-restricted-syntax
    expect(globalThis.localStorage?.length ?? 0).toBe(0);
    // eslint-disable-next-line no-restricted-properties, no-restricted-syntax
    expect(globalThis.sessionStorage?.length ?? 0).toBe(0);
  });

  it('inscrit puis connecte automatiquement le client', () => {
    let role: string | undefined;
    store
      .register({ email: 'n@b.ma', password: 'Secret1!', firstName: 'N', lastName: 'B' })
      .subscribe((u) => (role = u.role));
    const register = http.expectOne('/api/v1/auth/register');
    expect(register.request.body).toEqual({
      email: 'n@b.ma',
      password: 'Secret1!',
      firstName: 'N',
      lastName: 'B',
    });
    register.flush({}, { status: 201, statusText: 'Created' });
    const login = http.expectOne('/api/v1/auth/login');
    expect(login.request.body).toEqual({ email: 'n@b.ma', password: 'Secret1!' });
    login.flush(authResponse('Client'));
    expect(role).toBe('Client');
  });

  it('mutualise les refresh simultanés en une seule requête', () => {
    const tokens: string[] = [];
    store.refresh().subscribe((t) => tokens.push(t));
    store.refresh().subscribe((t) => tokens.push(t));
    store.refresh().subscribe((t) => tokens.push(t));
    http.expectOne('/api/v1/auth/refresh').flush(authResponse('Client', 'fresh'));
    expect(tokens).toEqual(['fresh', 'fresh', 'fresh']);
    // Une fois terminé, un nouveau refresh repart sur le réseau.
    store.refresh().subscribe();
    http.expectOne('/api/v1/auth/refresh').flush(authResponse('Client', 'fresh-2'));
    expect(store.accessToken()).toBe('fresh-2');
  });

  it('restaure la session au démarrage via le cookie', async () => {
    const restored = firstValueFrom(store.restoreSession());
    http.expectOne('/api/v1/auth/refresh').flush(authResponse('Admin'));
    expect(await restored).toBe(true);
    expect(store.user()).toEqual(USERS.Admin);
  });

  it('démarre anonyme si le refresh échoue, sans rediriger', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');
    const restored = firstValueFrom(store.restoreSession());
    http.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(await restored).toBe(false);
    expect(store.status()).toBe('anonymous');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('ferme la session et renvoie vers la connexion si le refresh échoue en cours de session', () => {
    signIn('Client');
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(router, 'url', 'get').mockReturnValue('/client/demandes?page=2');
    store.refresh().subscribe({ error: () => undefined });
    http.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(store.isAuthenticated()).toBe(false);
    expect(store.accessToken()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/connexion'], {
      queryParams: { returnUrl: '/client/demandes?page=2' },
    });
  });

  it('se déconnecte localement même si le serveur échoue', async () => {
    signIn('Client');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const done = firstValueFrom(store.logout());
    const req = http.expectOne('/api/v1/auth/logout');
    expect(req.request.withCredentials).toBe(true);
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-Client');
    req.flush(null, { status: 500, statusText: 'Server Error' });
    await done;
    expect(store.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/connexion');
  });
});
