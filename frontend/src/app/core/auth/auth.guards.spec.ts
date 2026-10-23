import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  PartialMatchRouteSnapshot,
  Route,
  Router,
  RouterStateSnapshot,
  UrlSegment,
  UrlTree,
} from '@angular/router';
import { provideTestEnvironment, signIn } from '../../../testing/test-helpers';
import { Role } from '../models/api.models';
import { guestGuard, roleGuard } from './auth.guards';

function runRoleGuard(roles: Role[], path: string[]) {
  const segments = path.map((p) => new UrlSegment(p, {}));
  return TestBed.runInInjectionContext(() =>
    roleGuard(...roles)({} as Route, segments, {} as PartialMatchRouteSnapshot),
  );
}

function serialize(result: unknown): string {
  return result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : String(result);
}

describe('Gardes de routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideTestEnvironment() });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('redirige un visiteur vers la connexion avec returnUrl', () => {
    expect(serialize(runRoleGuard(['Client'], ['client', 'profil']))).toBe(
      '/connexion?returnUrl=%2Fclient%2Fprofil',
    );
  });

  it('laisse passer le bon rôle', () => {
    signIn('Client');
    expect(runRoleGuard(['Client'], ['client'])).toBe(true);
  });

  it('refuse un rôle non autorisé (client vers analyste)', () => {
    signIn('Client');
    expect(serialize(runRoleGuard(['Analyst', 'Admin'], ['analyste']))).toBe('/acces-refuse');
  });

  it('autorise l’admin dans l’espace analyste mais pas l’analyste dans l’espace admin', () => {
    signIn('Admin');
    expect(runRoleGuard(['Analyst', 'Admin'], ['analyste'])).toBe(true);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: provideTestEnvironment() });
    signIn('Analyst');
    expect(serialize(runRoleGuard(['Admin'], ['admin']))).toBe('/acces-refuse');
  });

  it('renvoie un utilisateur connecté hors des pages invité', () => {
    const run = () =>
      TestBed.runInInjectionContext(() =>
        guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
      );
    expect(run()).toBe(true);
    signIn('Analyst');
    expect(serialize(run())).toBe('/analyste/file');
  });
});
