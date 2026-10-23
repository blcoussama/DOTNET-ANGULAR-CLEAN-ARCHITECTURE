import { HttpClient, HttpEvent, HttpEventType } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, filter, map } from 'rxjs';
import { API_BASE, toHttpParams } from '../http/api.config';
import {
  ApplicationDetail,
  ApplicationDocument,
  ApplicationDraft,
  CreditApplication,
  DocumentType,
  MyApplicationsQuery,
  Page,
} from '../models/api.models';

export type UploadProgress =
  { kind: 'progress'; percent: number } | { kind: 'done'; document: ApplicationDocument };

/** Demandes du client connecté (SPEC §3 « Demandes (client) »). */
@Injectable({ providedIn: 'root' })
export class ApplicationsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/applications`;

  private url(id: string, suffix = ''): string {
    return `${this.base}/${encodeURIComponent(id)}${suffix}`;
  }

  create(draft: ApplicationDraft): Observable<CreditApplication> {
    return this.http.post<CreditApplication>(this.base, draft);
  }

  listMine(query: MyApplicationsQuery): Observable<Page<CreditApplication>> {
    const params = toHttpParams({
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
    });
    return this.http.get<Page<CreditApplication>>(`${this.base}/mine`, { params });
  }

  get(id: string): Observable<ApplicationDetail> {
    return this.http.get<ApplicationDetail>(this.url(id)).pipe(
      map((detail) => ({
        ...detail,
        documents: detail.documents ?? [],
        events: detail.events ?? [],
      })),
    );
  }

  update(id: string, draft: ApplicationDraft): Observable<CreditApplication> {
    return this.http.put<CreditApplication>(this.url(id), draft);
  }

  submit(id: string): Observable<CreditApplication> {
    return this.http.post<CreditApplication>(this.url(id, '/submit'), {});
  }

  cancel(id: string): Observable<CreditApplication> {
    return this.http.post<CreditApplication>(this.url(id, '/cancel'), {});
  }

  /** Dépôt multipart (`type`, `file`) avec progression d'envoi. */
  uploadDocument(id: string, type: DocumentType, file: File): Observable<UploadProgress> {
    const form = new FormData();
    form.append('type', type);
    form.append('file', file, file.name);
    return this.http
      .post<ApplicationDocument>(this.url(id, '/documents'), form, {
        observe: 'events',
        reportProgress: true,
      })
      .pipe(
        filter(
          (event: HttpEvent<ApplicationDocument>) =>
            event.type === HttpEventType.UploadProgress || event.type === HttpEventType.Response,
        ),
        map((event): UploadProgress => {
          if (event.type === HttpEventType.UploadProgress) {
            const percent = event.total ? Math.round((100 * event.loaded) / event.total) : 0;
            return { kind: 'progress', percent };
          }
          if (event.type === HttpEventType.Response && event.body) {
            return { kind: 'done', document: event.body };
          }
          throw new Error('Réponse de dépôt inattendue');
        }),
      );
  }

  /** Téléchargement en Blob (propriétaire, analyste ou admin) : le jeton part via l'intercepteur. */
  downloadDocument(id: string, documentId: string): Observable<Blob> {
    return this.http.get(this.url(id, `/documents/${encodeURIComponent(documentId)}`), {
      responseType: 'blob',
    });
  }

  deleteDocument(id: string, documentId: string): Observable<void> {
    return this.http.delete<void>(this.url(id, `/documents/${encodeURIComponent(documentId)}`));
  }
}
