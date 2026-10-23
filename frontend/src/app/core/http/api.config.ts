import { HttpContextToken, HttpParams } from '@angular/common/http';

/** Préfixe relatif de l'API : servi par le proxy `ng serve` en dev, par le reverse proxy en prod. */
export const API_BASE = '/api/v1';

/**
 * Endpoints d'authentification anonymes : ils ne portent pas le jeton d'accès (le refresh s'appuie
 * sur le cookie httpOnly). `/auth/logout` et `/auth/me` le portent. Aucun `/auth/*` ne déclenche
 * de refresh sur 401.
 */
export const AUTH_ANONYMOUS_ENDPOINTS = [
  `${API_BASE}/auth/login`,
  `${API_BASE}/auth/register`,
  `${API_BASE}/auth/refresh`,
];

/** Marque une requête déjà rejouée après refresh, pour éviter toute boucle. */
export const AUTH_RETRIED = new HttpContextToken<boolean>(() => false);

/** Permet à un appel de désactiver le refresh automatique sur 401. */
export const SKIP_AUTH_REFRESH = new HttpContextToken<boolean>(() => false);

export function isApiUrl(url: string): boolean {
  return url === API_BASE || url.startsWith(`${API_BASE}/`);
}

export function isAnonymousAuthUrl(url: string): boolean {
  const path = url.split('?')[0];
  return AUTH_ANONYMOUS_ENDPOINTS.includes(path);
}

export function isAuthUrl(url: string): boolean {
  return url.startsWith(`${API_BASE}/auth/`);
}

type ParamValue = string | number | boolean | null | undefined;

/** Construit des `HttpParams` en ignorant les valeurs vides (`null`, `undefined`, `''`, `false` optionnel). */
export function toHttpParams(
  values: Record<string, ParamValue>,
  options: { dropFalse?: boolean } = {},
): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === null || value === undefined) continue;
    if (typeof value === 'string' && value.trim() === '') continue;
    if (value === false && options.dropFalse) continue;
    params = params.set(key, typeof value === 'string' ? value.trim() : String(value));
  }
  return params;
}
