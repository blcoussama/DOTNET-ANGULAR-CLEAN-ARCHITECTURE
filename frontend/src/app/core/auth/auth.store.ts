import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  Observable,
  catchError,
  finalize,
  firstValueFrom,
  map,
  of,
  shareReplay,
  switchMap,
  tap,
  timeout,
} from 'rxjs';
import { AuthApi } from '../api/auth.api';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest, Role } from '../models/api.models';
import { NotificationService } from '../notify/notification.service';

export type AuthStatus = 'unknown' | 'authenticated' | 'anonymous';

/** Verrou inter-onglets du refresh. */
const REFRESH_LOCK = 'creditflow-auth-refresh';

/** Délai maximal de restauration de session au démarrage (le backend peut être injoignable). */
const RESTORE_TIMEOUT_MS = 8000;

/**
 * État d'authentification, en mémoire uniquement.
 *
 * - Le jeton d'accès n'est **jamais** écrit dans `localStorage` / `sessionStorage` : il vit dans un
 *   signal et disparaît à la fermeture de l'onglet. La session est restaurée au démarrage via
 *   `POST /auth/refresh`, qui s'appuie sur le cookie httpOnly `refreshToken`.
 * - `refresh()` mutualise les appels concurrents : un seul refresh réseau à la fois (le backend fait
 *   une rotation avec détection de rejeu, deux refresh parallèles révoqueraient la session).
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);

  private readonly token = signal<string | null>(null);
  private readonly currentUser = signal<AuthUser | null>(null);
  private readonly authStatus = signal<AuthStatus>('unknown');
  private refreshInFlight: Observable<string> | null = null;

  readonly user = this.currentUser.asReadonly();
  readonly status = this.authStatus.asReadonly();
  readonly isAuthenticated = computed(
    () => this.authStatus() === 'authenticated' && this.currentUser() !== null,
  );
  readonly role = computed<Role | null>(() => this.currentUser()?.role ?? null);
  readonly displayName = computed(() => {
    const user = this.currentUser();
    return user ? `${user.firstName} ${user.lastName}`.trim() || user.email : '';
  });
  readonly initials = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return (
      `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase() ||
      user.email.charAt(0).toUpperCase()
    );
  });

  /** Jeton d'accès courant (lu par l'intercepteur, jamais persisté). */
  accessToken(): string | null {
    return this.token();
  }

  hasRole(...roles: Role[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  login(credentials: LoginRequest): Observable<AuthUser> {
    return this.api.login(credentials).pipe(
      tap((response) => this.setSession(response)),
      map((response) => response.user),
    );
  }

  /** Inscription (rôle Client) puis connexion automatique. */
  register(request: RegisterRequest): Observable<AuthUser> {
    return this.api
      .register(request)
      .pipe(switchMap(() => this.login({ email: request.email, password: request.password })));
  }

  /**
   * Rafraîchit le jeton d'accès via le cookie httpOnly. Les appels simultanés partagent la même
   * requête réseau. En cas d'échec, la session est fermée.
   */
  refresh(): Observable<string> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.refreshAcrossTabs().pipe(
        tap({
          next: (response) => this.setSession(response),
          error: () => this.onRefreshFailed(),
        }),
        map((response) => response.accessToken),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }
    return this.refreshInFlight;
  }

  /**
   * Sérialise le refresh entre onglets (Web Locks API) : avec la rotation du refresh token et la
   * détection de rejeu côté serveur, deux onglets qui rafraîchiraient en même temps avec le même
   * cookie feraient révoquer toute la session. Repli sans verrou si l'API est indisponible.
   */
  private refreshAcrossTabs(): Observable<AuthResponse> {
    const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined;
    if (!locks?.request) return this.api.refresh();
    return new Observable<AuthResponse>((subscriber) => {
      const abort = new AbortController();
      locks
        .request(REFRESH_LOCK, { signal: abort.signal }, () => firstValueFrom(this.api.refresh()))
        .then(
          (response) => {
            subscriber.next(response);
            subscriber.complete();
          },
          (error: unknown) => {
            if (!abort.signal.aborted) subscriber.error(error);
          },
        );
      return () => abort.abort();
    });
  }

  /** Restauration silencieuse au démarrage : ne lève jamais d'erreur. */
  restoreSession(): Observable<boolean> {
    return this.refresh().pipe(
      timeout(RESTORE_TIMEOUT_MS),
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      }),
    );
  }

  /** Déconnexion : révoque le refresh côté serveur (best effort) puis vide l'état local. */
  logout(): Observable<void> {
    const finish = () => {
      this.clearSession();
      void this.router.navigateByUrl('/connexion');
    };
    return this.api.logout().pipe(
      map(() => undefined),
      catchError(() => of(undefined)),
      tap(() => finish()),
    );
  }

  /** Utilisé quand une action échoue définitivement en 401 (ex. compte désactivé). */
  expireSession(): void {
    const wasAuthenticated = this.authStatus() === 'authenticated';
    this.clearSession();
    if (wasAuthenticated) {
      this.notify.info('Votre session a expiré. Veuillez vous reconnecter.');
      const returnUrl = this.router.url;
      void this.router.navigate(['/connexion'], {
        queryParams: returnUrl && returnUrl !== '/' ? { returnUrl } : {},
      });
    }
  }

  private onRefreshFailed(): void {
    if (this.authStatus() === 'authenticated') {
      this.expireSession();
    } else {
      this.clearSession();
    }
  }

  private setSession(response: AuthResponse): void {
    this.token.set(response.accessToken);
    this.currentUser.set(response.user);
    this.authStatus.set('authenticated');
  }

  private clearSession(): void {
    this.token.set(null);
    this.currentUser.set(null);
    this.authStatus.set('anonymous');
  }
}
