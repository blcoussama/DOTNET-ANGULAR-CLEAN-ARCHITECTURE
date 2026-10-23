import { Role } from '../models/api.models';

const AUTH_PAGES = ['/connexion', '/inscription'];

/**
 * N'accepte qu'un chemin interne à l'application (`/client/demandes?page=2`).
 * Refuse les URL absolues, protocol-relative (`//evil.com`), les antislashs, les schémas
 * (`javascript:`), les caractères de contrôle et les pages d'authentification (boucle).
 */
export function sanitizeReturnUrl(value: unknown): string | null {
  if (typeof value !== 'string') return null;
  const url = value.trim();
  if (!url || url.length > 2048) return null;
  if (!url.startsWith('/') || url.startsWith('//')) return null;
  if (url.includes('\\')) return null;
  // eslint-disable-next-line no-control-regex
  if (/[\u0000-\u001f\u007f]/.test(url)) return null;
  const path = url.split(/[?#]/)[0];
  if (path.includes(':')) return null;
  if (AUTH_PAGES.some((p) => path === p || path.startsWith(`${p}/`))) return null;
  return url;
}

/** Espace d'accueil par rôle. */
export function homeUrlFor(role: Role | null | undefined): string {
  switch (role) {
    case 'Client':
      return '/client/demandes';
    case 'Analyst':
      return '/analyste/file';
    case 'Admin':
      return '/admin/utilisateurs';
    default:
      return '/';
  }
}

/** Vérifie qu'un chemin interne correspond à un espace autorisé pour ce rôle. */
export function isUrlAllowedForRole(url: string, role: Role): boolean {
  const path = url.split(/[?#]/)[0];
  const area = (prefix: string) => path === prefix || path.startsWith(`${prefix}/`);
  if (area('/client')) return role === 'Client';
  if (area('/analyste')) return role === 'Analyst' || role === 'Admin';
  if (area('/admin')) return role === 'Admin';
  return true;
}

/** Destination après connexion : `returnUrl` sûr et autorisé, sinon l'accueil du rôle. */
export function resolvePostLoginUrl(returnUrl: unknown, role: Role): string {
  const safe = sanitizeReturnUrl(returnUrl);
  if (safe && safe !== '/' && isUrlAllowedForRole(safe, role)) return safe;
  return homeUrlFor(role);
}
