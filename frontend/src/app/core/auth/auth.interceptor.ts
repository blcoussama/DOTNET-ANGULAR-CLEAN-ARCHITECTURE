import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import {
  AUTH_RETRIED,
  SKIP_AUTH_REFRESH,
  isAnonymousAuthUrl,
  isApiUrl,
  isAuthUrl,
} from '../http/api.config';
import { AuthStore } from './auth.store';

function withBearer<T>(req: HttpRequest<T>, token: string): HttpRequest<T> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/**
 * Intercepteur fonctionnel d'authentification :
 * 1. n'agit que sur l'API relative `/api/v1` (aucun jeton n'est envoyé vers un autre domaine) ;
 * 2. `withCredentials` sur `/auth/*` pour le cookie httpOnly de refresh ;
 * 3. ajoute `Authorization: Bearer` quand un jeton est en mémoire ;
 * 4. sur 401 (hors `/auth/*`), déclenche **un seul** refresh partagé puis rejoue la requête une fois
 *    (si le jeton a déjà été renouvelé entre-temps par un autre appel, rejoue directement).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApiUrl(req.url)) {
    return next(req);
  }
  const store = inject(AuthStore);

  let request = isAuthUrl(req.url) ? req.clone({ withCredentials: true }) : req;
  const token = store.accessToken();
  if (token && !isAnonymousAuthUrl(req.url)) {
    request = withBearer(request, token);
  }

  return next(request).pipe(
    catchError((error: unknown) => {
      const canRefresh =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !isAuthUrl(req.url) &&
        !req.context.get(AUTH_RETRIED) &&
        !req.context.get(SKIP_AUTH_REFRESH);
      if (!canRefresh) {
        return throwError(() => error);
      }
      // Un autre appel a déjà renouvelé le jeton pendant que celui-ci était en vol : simple rejeu.
      const current = store.accessToken();
      if (token && current && current !== token) {
        req.context.set(AUTH_RETRIED, true);
        return next(withBearer(req, current));
      }
      return store.refresh().pipe(
        // Échec du refresh : la session est fermée par le store ; on propage l'erreur d'origine.
        catchError(() => throwError(() => error)),
        switchMap((freshToken) => {
          req.context.set(AUTH_RETRIED, true);
          return next(withBearer(req, freshToken));
        }),
      );
    }),
  );
};
