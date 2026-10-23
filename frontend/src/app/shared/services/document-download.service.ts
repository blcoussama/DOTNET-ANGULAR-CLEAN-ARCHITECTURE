import { Injectable, inject, signal } from '@angular/core';
import { Observable, finalize, map, tap } from 'rxjs';
import { ApplicationsApi } from '../../core/api/applications.api';
import { ApplicationDocument } from '../../core/models/api.models';
import { NotificationService } from '../../core/notify/notification.service';
import { saveBlob } from '../../core/util/download';

/** Téléchargement authentifié d'une pièce (Blob via HttpClient). */
@Injectable({ providedIn: 'root' })
export class DocumentDownloadService {
  private readonly api = inject(ApplicationsApi);
  private readonly notify = inject(NotificationService);
  /** Identifiant de la pièce en cours de téléchargement. */
  readonly busyId = signal<string | null>(null);

  download(applicationId: string, doc: ApplicationDocument): Observable<void> {
    this.busyId.set(doc.id);
    return this.api.downloadDocument(applicationId, doc.id).pipe(
      tap({
        next: (blob) => saveBlob(blob, doc.fileName),
        error: (error: unknown) => this.notify.error(error),
      }),
      map(() => undefined),
      finalize(() => this.busyId.set(null)),
    );
  }
}
