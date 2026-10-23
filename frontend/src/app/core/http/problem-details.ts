import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';

/** ProblemDetails (RFC 7807) tel que renvoyé par ASP.NET Core. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[] | string>;
  traceId?: string;
}

/** Erreur API normalisée pour l'affichage. */
export interface ApiError {
  status: number;
  /** Message principal, en français, prêt à afficher. */
  message: string;
  /** Erreurs de validation par champ (clés normalisées en camelCase). */
  fieldErrors: Record<string, string[]>;
  problem: ProblemDetails | null;
}

const STATUS_MESSAGES: Record<number, string> = {
  0: 'Impossible de joindre le serveur. Vérifiez votre connexion et réessayez.',
  400: 'Certaines informations sont invalides.',
  401: 'Votre session a expiré. Veuillez vous reconnecter.',
  403: "Vous n'avez pas les droits nécessaires pour cette action.",
  404: 'Ressource introuvable.',
  409: "L'opération n'est pas possible dans l'état actuel du dossier.",
  413: 'Le fichier est trop volumineux.',
  415: 'Type de fichier non pris en charge.',
  422: 'Certaines informations sont invalides.',
  429: 'Trop de tentatives. Patientez quelques instants avant de réessayer.',
  500: 'Une erreur inattendue est survenue côté serveur.',
  503: 'Le service est momentanément indisponible.',
};

/** Normalise une clé d'erreur ASP.NET (`MonthlyIncome`, `$.monthlyIncome`, `Profile.City`) en camelCase. */
export function normalizeFieldKey(key: string): string {
  const cleaned = key.replace(/^\$\.?/, '').trim();
  const last = cleaned.split('.').pop() ?? cleaned;
  const withoutIndex = last.replace(/\[\d+\]$/, '');
  return withoutIndex.charAt(0).toLowerCase() + withoutIndex.slice(1);
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    typeof value === 'object' &&
    value !== null &&
    ('title' in value || 'detail' in value || 'errors' in value || 'status' in value)
  );
}

/** Les erreurs de `resource()` enveloppent l'erreur HTTP d'origine dans `cause`. */
function unwrapError(error: unknown): unknown {
  let current = error;
  for (let depth = 0; depth < 3; depth++) {
    if (current instanceof HttpErrorResponse) return current;
    if (typeof current === 'object' && current !== null && 'cause' in current) {
      current = (current as { cause: unknown }).cause;
    } else {
      break;
    }
  }
  return current;
}

export function toApiError(rawError: unknown): ApiError {
  const error = unwrapError(rawError);
  if (!(error instanceof HttpErrorResponse)) {
    return {
      status: -1,
      message: 'Une erreur inattendue est survenue.',
      fieldErrors: {},
      problem: null,
    };
  }
  const status = error.status;
  const body: unknown = error.error;
  const problem = isProblemDetails(body) ? body : null;
  const fieldErrors: Record<string, string[]> = {};
  if (problem?.errors) {
    for (const [key, raw] of Object.entries(problem.errors)) {
      const messages = (Array.isArray(raw) ? raw : [raw]).filter(
        (m): m is string => typeof m === 'string',
      );
      if (!messages.length) continue;
      const normalized = normalizeFieldKey(key) || '_';
      fieldErrors[normalized] = [...(fieldErrors[normalized] ?? []), ...messages];
    }
  }
  const fallback =
    STATUS_MESSAGES[status] ?? (status >= 500 ? STATUS_MESSAGES[500] : 'La requête a échoué.');
  // Le `detail` est le message métier explicite (ex. transition refusée) ; le `title` générique en anglais
  // d'ASP.NET (« One or more validation errors occurred. ») n'est pas affiché tel quel.
  const detail = problem?.detail?.trim();
  const title = problem?.title?.trim();
  const genericTitle =
    !title ||
    /validation errors occurred|^(Bad Request|Not Found|Conflict|Unauthorized|Forbidden|Internal Server Error)$/i.test(
      title,
    );
  const message = detail || (!genericTitle && title ? title : fallback);
  return { status, message, fieldErrors, problem };
}

/** Rend le message de l'erreur (texte simple, jamais du HTML). */
export function errorMessage(error: unknown): string {
  return toApiError(error).message;
}

/**
 * Reporte les erreurs de validation serveur sur les contrôles du formulaire (erreur `server`).
 * Renvoie les messages qui n'ont pu être associés à aucun contrôle.
 */
export function applyServerErrors(form: FormGroup, apiError: ApiError): string[] {
  const unmatched: string[] = [];
  for (const [key, messages] of Object.entries(apiError.fieldErrors)) {
    const control: AbstractControl | null = form.get(key);
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}
