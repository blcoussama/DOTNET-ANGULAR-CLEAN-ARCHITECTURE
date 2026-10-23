import { isAnonymousAuthUrl, isApiUrl, isAuthUrl, toHttpParams } from './api.config';

describe('api.config', () => {
  it('construit des paramètres en ignorant les valeurs vides', () => {
    const params = toHttpParams({
      page: 2,
      status: null,
      search: '  ',
      riskGrade: undefined,
      q: ' dupont ',
    });
    expect(params.keys()).toEqual(['page', 'q']);
    expect(params.get('q')).toBe('dupont');
  });

  it('peut ignorer les booléens faux', () => {
    expect(toHttpParams({ assignedToMe: false }, { dropFalse: true }).has('assignedToMe')).toBe(
      false,
    );
    expect(toHttpParams({ assignedToMe: true }, { dropFalse: true }).get('assignedToMe')).toBe(
      'true',
    );
  });

  it('reconnaît les URL de l’API relative uniquement', () => {
    expect(isApiUrl('/api/v1/products')).toBe(true);
    expect(isApiUrl('https://evil.example/api/v1/products')).toBe(false);
    expect(isApiUrl('/api/v10/x')).toBe(false);
  });

  it('distingue les endpoints d’authentification anonymes', () => {
    expect(isAnonymousAuthUrl('/api/v1/auth/login')).toBe(true);
    expect(isAnonymousAuthUrl('/api/v1/auth/refresh')).toBe(true);
    expect(isAnonymousAuthUrl('/api/v1/auth/me')).toBe(false);
    expect(isAuthUrl('/api/v1/auth/me')).toBe(true);
  });
});
