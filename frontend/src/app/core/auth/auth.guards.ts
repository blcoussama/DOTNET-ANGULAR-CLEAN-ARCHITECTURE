import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router, UrlSegment } from '@angular/router';
import { Role } from '../models/api.models';
import { homeUrlFor } from '../util/safe-redirect';
import { AuthStore } from './auth.store';

function currentPath(segments: UrlSegment[]): string {
  return '/' + segments.map((s) => s.path).join('/');
}

/**
 * Garde par rôle, en `canMatch` : le code de l'espace n'est même pas téléchargé si l'utilisateur
 * n'y a pas droit. Non connecté ⇒ `/connexion?returnUrl=…` ; mauvais rôle ⇒ `/acces-refuse`.
 */
export function roleGuard(...roles: Role[]): CanMatchFn {
  return (_route, segments) => {
    const store = inject(AuthStore);
    const router = inject(Router);
    if (!store.isAuthenticated()) {
      const navigation = router.currentNavigation();
      const returnUrl = navigation
        ? router.serializeUrl(navigation.extractedUrl)
        : currentPath(segments);
      return router.createUrlTree(['/connexion'], { queryParams: { returnUrl } });
    }
    return store.hasRole(...roles) ? true : router.createUrlTree(['/acces-refuse']);
  };
}

/** Pages invité (connexion, inscription) : un utilisateur connecté est renvoyé vers son espace. */
export const guestGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  return store.isAuthenticated() ? router.parseUrl(homeUrlFor(store.role())) : true;
};
