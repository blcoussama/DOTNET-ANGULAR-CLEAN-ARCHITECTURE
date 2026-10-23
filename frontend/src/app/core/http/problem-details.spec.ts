import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';
import { applyServerErrors, errorMessage, normalizeFieldKey, toApiError } from './problem-details';

function httpError(status: number, error: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error, url: '/api/v1/x' });
}

describe('ProblemDetails', () => {
  it('normalise les clés de champ ASP.NET en camelCase', () => {
    expect(normalizeFieldKey('MonthlyIncome')).toBe('monthlyIncome');
    expect(normalizeFieldKey('$.monthlyCharges')).toBe('monthlyCharges');
    expect(normalizeFieldKey('Profile.City')).toBe('city');
    expect(normalizeFieldKey('Items[0]')).toBe('items');
  });

  it('extrait les erreurs de validation par champ (400)', () => {
    const error = toApiError(
      httpError(400, {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { MonthlyIncome: ['Doit être > 0.'], '$.city': 'Obligatoire' },
      }),
    );
    expect(error.status).toBe(400);
    expect(error.fieldErrors).toEqual({ monthlyIncome: ['Doit être > 0.'], city: ['Obligatoire'] });
    // Le titre générique anglais n'est pas affiché.
    expect(error.message).toBe('Certaines informations sont invalides.');
  });

  it('privilégie le `detail` métier (ex. transition refusée en 409)', () => {
    const error = toApiError(
      httpError(409, {
        title: 'Conflict',
        status: 409,
        detail: 'Transition APPROVED → SUBMITTED interdite.',
      }),
    );
    expect(error.message).toBe('Transition APPROVED → SUBMITTED interdite.');
  });

  it('utilise un message français par statut quand le corps est vide', () => {
    expect(toApiError(httpError(0, null)).message).toContain('Impossible de joindre le serveur');
    expect(toApiError(httpError(429, null)).message).toContain('Trop de tentatives');
    expect(toApiError(httpError(502, null)).message).toContain('erreur inattendue');
  });

  it('déballe une erreur enveloppée par resource() (cause)', () => {
    const wrapped = new Error('wrapped', {
      cause: httpError(404, { title: 'Not Found', status: 404 }),
    });
    expect(toApiError(wrapped).status).toBe(404);
    expect(errorMessage(wrapped)).toBe('Ressource introuvable.');
  });

  it('gère une erreur non HTTP', () => {
    expect(toApiError(new Error('boom')).status).toBe(-1);
  });

  it('reporte les erreurs serveur sur les contrôles et renvoie les messages orphelins', () => {
    const form = new FormGroup({ city: new FormControl('x'), phone: new FormControl('y') });
    const unmatched = applyServerErrors(form, {
      status: 400,
      message: '',
      problem: null,
      fieldErrors: { city: ['Ville inconnue'], other: ['Erreur globale'] },
    });
    expect(form.controls.city.getError('server')).toBe('Ville inconnue');
    expect(form.controls.city.touched).toBe(true);
    expect(form.controls.phone.errors).toBeNull();
    expect(unmatched).toEqual(['Erreur globale']);
  });
});
