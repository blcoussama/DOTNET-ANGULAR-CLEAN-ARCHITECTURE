import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { API_BASE } from '../http/api.config';
import { ClientProfile, ClientProfileUpdate } from '../models/api.models';

export const EMPTY_PROFILE: ClientProfile = {
  dateOfBirth: null,
  phone: null,
  city: null,
  employmentType: null,
  employer: null,
  monthlyIncome: null,
  monthlyCharges: null,
  yearsAtCurrentJob: null,
  isComplete: false,
};

/** `yyyy-MM-dd` quelle que soit la forme renvoyée (DateOnly ou DateTime ISO). */
export function toDateOnly(value: string | null | undefined): string | null {
  if (!value) return null;
  const match = /^(\d{4}-\d{2}-\d{2})/.exec(value);
  return match ? match[1] : null;
}

function normalizeProfile(profile: ClientProfile | null): ClientProfile {
  if (!profile) return { ...EMPTY_PROFILE };
  return { ...EMPTY_PROFILE, ...profile, dateOfBirth: toDateOnly(profile.dateOfBirth) };
}

@Injectable({ providedIn: 'root' })
export class ProfileApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${API_BASE}/clients/me/profile`;

  /** Profil du client connecté ; un profil encore inexistant (404 / 204) est renvoyé vide. */
  getMine(): Observable<ClientProfile> {
    return this.http.get<ClientProfile | null>(this.url).pipe(
      map((profile) => normalizeProfile(profile)),
      catchError((error: unknown) =>
        error instanceof HttpErrorResponse && error.status === 404
          ? of({ ...EMPTY_PROFILE })
          : throwError(() => error),
      ),
    );
  }

  updateMine(body: ClientProfileUpdate): Observable<ClientProfile> {
    return this.http
      .put<ClientProfile>(this.url, body)
      .pipe(map((profile) => normalizeProfile(profile)));
  }
}
