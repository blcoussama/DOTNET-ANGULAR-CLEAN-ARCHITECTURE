import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authResponse, provideTestEnvironment, signIn } from '../../../testing/test-helpers';
import { AuthStore } from './auth.store';

describe('authInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideTestEnvironment() });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  it('ajoute le jeton Bearer sur les appels API', () => {
    signIn('Client', 'jwt-a');
    client.get('/api/v1/applications/mine').subscribe();
    expect(http.expectOne('/api/v1/applications/mine').request.headers.get('Authorization')).toBe(
      'Bearer jwt-a',
    );
  });

  it('n’envoie jamais le jeton vers une autre origine', () => {
    signIn('Client', 'jwt-a');
    client.get('https://cdn.example.com/data.json').subscribe();
    expect(
      http.expectOne('https://cdn.example.com/data.json').request.headers.has('Authorization'),
    ).toBe(false);
  });

  it('n’ajoute pas le jeton sur login / refresh et force withCredentials sur /auth', () => {
    signIn('Client', 'jwt-a');
    client.post('/api/v1/auth/refresh', {}).subscribe();
    const req = http.expectOne('/api/v1/auth/refresh');
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(req.request.withCredentials).toBe(true);
    req.flush(authResponse('Client'));
  });

  it('sur 401 : un seul refresh partagé, puis rejeu des requêtes avec le nouveau jeton', () => {
    signIn('Client', 'old');
    const results: string[] = [];
    client.get<string>('/api/v1/a').subscribe((r) => results.push(r));
    client.get<string>('/api/v1/b').subscribe((r) => results.push(r));
    http.expectOne('/api/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/b').flush(null, { status: 401, statusText: 'Unauthorized' });

    const refreshes = http.match('/api/v1/auth/refresh');
    expect(refreshes).toHaveLength(1);
    refreshes[0].flush(authResponse('Client', 'new'));

    const retryA = http.expectOne('/api/v1/a');
    const retryB = http.expectOne('/api/v1/b');
    expect(retryA.request.headers.get('Authorization')).toBe('Bearer new');
    expect(retryB.request.headers.get('Authorization')).toBe('Bearer new');
    retryA.flush('A');
    retryB.flush('B');
    expect(results.sort()).toEqual(['A', 'B']);
  });

  it('rejoue sans nouveau refresh une requête partie avec un jeton déjà renouvelé', () => {
    signIn('Client', 'old');
    let result = '';
    client.get('/api/v1/a').subscribe();
    client.get<string>('/api/v1/b').subscribe((r) => (result = r));
    http.expectOne('/api/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/auth/refresh').flush(authResponse('Client', 'new'));
    http.expectOne('/api/v1/a').flush('A');
    // B était parti avec l'ancien jeton et revient en 401 après le refresh.
    http.expectOne('/api/v1/b').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectNone('/api/v1/auth/refresh');
    const retry = http.expectOne('/api/v1/b');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    retry.flush('B');
    expect(result).toBe('B');
  });

  it('ne rejoue qu’une fois : un second 401 est propagé sans nouveau refresh', () => {
    signIn('Client', 'old');
    let status = 0;
    client.get('/api/v1/a').subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });
    http.expectOne('/api/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/auth/refresh').flush(authResponse('Client', 'new'));
    http.expectOne('/api/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectNone('/api/v1/auth/refresh');
    expect(status).toBe(401);
  });

  it('si le refresh échoue : erreur d’origine propagée et session fermée', () => {
    const store = signIn('Client', 'old');
    let status = 0;
    client.get('/api/v1/a').subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });
    http.expectOne('/api/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(status).toBe(401);
    expect(store.isAuthenticated()).toBe(false);
    expect(TestBed.inject(AuthStore).accessToken()).toBeNull();
  });

  it('ne tente pas de refresh sur un 401 de /auth/login', () => {
    let status = 0;
    client
      .post('/api/v1/auth/login', {})
      .subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });
    http.expectOne('/api/v1/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectNone('/api/v1/auth/refresh');
    expect(status).toBe(401);
  });

  it('laisse passer les autres erreurs sans refresh', () => {
    signIn('Client');
    let status = 0;
    client.get('/api/v1/a').subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });
    http.expectOne('/api/v1/a').flush(null, { status: 403, statusText: 'Forbidden' });
    http.expectNone('/api/v1/auth/refresh');
    expect(status).toBe(403);
  });
});
