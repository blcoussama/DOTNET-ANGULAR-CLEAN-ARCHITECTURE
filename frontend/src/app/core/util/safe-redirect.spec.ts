import {
  homeUrlFor,
  isUrlAllowedForRole,
  resolvePostLoginUrl,
  sanitizeReturnUrl,
} from './safe-redirect';

describe('safe-redirect', () => {
  it('accepte un chemin interne avec paramètres', () => {
    expect(sanitizeReturnUrl('/client/demandes?page=2')).toBe('/client/demandes?page=2');
  });

  it.each([
    ['https://evil.example/phish'],
    ['//evil.example'],
    ['/\\evil.example'],
    ['javascript:alert(1)'],
    ['/x:y'],
    ['/client\u0000'],
    ['relative/path'],
    ['/connexion?returnUrl=/admin'],
    [''],
  ])('refuse %s', (value) => {
    expect(sanitizeReturnUrl(value)).toBeNull();
  });

  it('refuse les valeurs non textuelles', () => {
    expect(sanitizeReturnUrl(['/client'])).toBeNull();
    expect(sanitizeReturnUrl(undefined)).toBeNull();
  });

  it('associe chaque rôle à son espace', () => {
    expect(homeUrlFor('Client')).toBe('/client/demandes');
    expect(homeUrlFor('Analyst')).toBe('/analyste/file');
    expect(homeUrlFor('Admin')).toBe('/admin/utilisateurs');
    expect(homeUrlFor(null)).toBe('/');
  });

  it('vérifie l’espace autorisé par rôle (l’admin lit l’espace analyste)', () => {
    expect(isUrlAllowedForRole('/analyste/file', 'Admin')).toBe(true);
    expect(isUrlAllowedForRole('/admin/produits', 'Analyst')).toBe(false);
    expect(isUrlAllowedForRole('/client/profil', 'Analyst')).toBe(false);
    expect(isUrlAllowedForRole('/', 'Client')).toBe(true);
  });

  it('résout la destination après connexion', () => {
    expect(resolvePostLoginUrl('/client/profil', 'Client')).toBe('/client/profil');
    expect(resolvePostLoginUrl('/admin/utilisateurs', 'Client')).toBe('/client/demandes');
    expect(resolvePostLoginUrl('//evil.example', 'Analyst')).toBe('/analyste/file');
  });
});
