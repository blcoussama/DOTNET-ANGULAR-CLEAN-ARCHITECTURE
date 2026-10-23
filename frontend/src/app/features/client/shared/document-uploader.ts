import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ApplicationsApi } from '../../../core/api/applications.api';
import { errorMessage } from '../../../core/http/problem-details';
import { DOCUMENT_LABELS } from '../../../core/i18n/labels';
import { ApplicationDocument, DOCUMENT_TYPES, DocumentType } from '../../../core/models/api.models';
import { NotificationService } from '../../../core/notify/notification.service';
import {
  ACCEPT_ATTRIBUTE,
  FILE_ERROR_MESSAGES,
  validateFileDeep,
} from '../../../core/util/file-validation';
import { confirmAction } from '../../../shared/components/confirm-dialog';
import { DocumentList } from '../../../shared/components/document-list';
import { DocumentDownloadService } from '../../../shared/services/document-download.service';

/** Pièces exigées avant soumission (SPEC §3 : au moins une `ID_CARD` et une `PAYSLIP`). */
export const REQUIRED_DOCUMENT_TYPES: DocumentType[] = ['ID_CARD', 'PAYSLIP'];

export function missingRequiredDocuments(documents: ApplicationDocument[]): DocumentType[] {
  return REQUIRED_DOCUMENT_TYPES.filter((type) => !documents.some((doc) => doc.type === type));
}

interface UploadState {
  fileName: string;
  percent: number;
}

/**
 * Dépôt des pièces par type : glisser-déposer ou sélection, contrôle côté client (5 Mo, PDF/JPEG/PNG,
 * signature binaire), progression d'envoi, suppression. Le serveur reste l'autorité.
 */
@Component({
  selector: 'app-document-uploader',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, DocumentList],
  templateUrl: './document-uploader.html',
  styleUrl: './document-uploader.scss',
})
export class DocumentUploader {
  private readonly api = inject(ApplicationsApi);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  protected readonly downloader = inject(DocumentDownloadService);

  readonly applicationId = input.required<string>();
  readonly documents = input<ApplicationDocument[]>([]);
  readonly editable = input(true);
  /** Pièce déposée avec succès (le parent l'ajoute à sa liste). */
  readonly uploaded = output<ApplicationDocument>();
  /** Pièce supprimée. */
  readonly removed = output<ApplicationDocument>();

  protected readonly types = DOCUMENT_TYPES;
  protected readonly labels = DOCUMENT_LABELS;
  protected readonly accept = ACCEPT_ATTRIBUTE;
  protected readonly required = new Set<DocumentType>(REQUIRED_DOCUMENT_TYPES);

  protected readonly uploads = signal<Partial<Record<DocumentType, UploadState>>>({});
  protected readonly errors = signal<Partial<Record<DocumentType, string>>>({});
  protected readonly dragOver = signal<DocumentType | null>(null);
  protected readonly deletingId = signal<string | null>(null);

  protected readonly byType = computed(() => {
    const groups = Object.fromEntries(
      DOCUMENT_TYPES.map((t) => [t, [] as ApplicationDocument[]]),
    ) as Record<DocumentType, ApplicationDocument[]>;
    for (const doc of this.documents()) groups[doc.type]?.push(doc);
    return groups;
  });
  protected readonly missing = computed(() => missingRequiredDocuments(this.documents()));
  protected readonly busyId = computed(() => this.deletingId() ?? this.downloader.busyId());

  protected onDragOver(event: DragEvent, type: DocumentType): void {
    if (!this.editable()) return;
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy';
    this.dragOver.set(type);
  }

  protected onDragLeave(type: DocumentType): void {
    if (this.dragOver() === type) this.dragOver.set(null);
  }

  protected onDrop(event: DragEvent, type: DocumentType): void {
    event.preventDefault();
    this.dragOver.set(null);
    if (!this.editable()) return;
    const files = Array.from(event.dataTransfer?.files ?? []);
    void this.handleFiles(type, files);
  }

  protected onFileInput(event: Event, type: DocumentType): void {
    const inputEl = event.target as HTMLInputElement;
    const files = Array.from(inputEl.files ?? []);
    inputEl.value = '';
    void this.handleFiles(type, files);
  }

  /** Valide puis envoie les fichiers un par un. */
  async handleFiles(type: DocumentType, files: File[]): Promise<void> {
    if (!files.length || this.uploads()[type]) return;
    this.setError(type, null);
    for (const file of files) {
      const problem = await validateFileDeep(file);
      if (problem) {
        this.setError(type, `${file.name} : ${FILE_ERROR_MESSAGES[problem]}`);
        continue;
      }
      await this.upload(type, file);
    }
  }

  private upload(type: DocumentType, file: File): Promise<void> {
    this.uploads.update((u) => ({ ...u, [type]: { fileName: file.name, percent: 0 } }));
    return new Promise<void>((resolve) => {
      this.api.uploadDocument(this.applicationId(), type, file).subscribe({
        next: (event) => {
          if (event.kind === 'progress') {
            this.uploads.update((u) => ({
              ...u,
              [type]: { fileName: file.name, percent: event.percent },
            }));
          } else {
            this.uploaded.emit(event.document);
            this.notify.success(`${this.labels[type]} déposé(e).`);
          }
        },
        error: (error: unknown) => {
          this.clearUpload(type);
          this.setError(type, `${file.name} : ${errorMessage(error)}`);
          resolve();
        },
        complete: () => {
          this.clearUpload(type);
          resolve();
        },
      });
    });
  }

  protected download(doc: ApplicationDocument): void {
    this.downloader.download(this.applicationId(), doc).subscribe({ error: () => undefined });
  }

  protected remove(doc: ApplicationDocument): void {
    confirmAction(this.dialog, {
      title: 'Supprimer la pièce ?',
      message: `« ${doc.fileName} » sera définitivement retirée du dossier.`,
      confirmLabel: 'Supprimer',
      destructive: true,
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.deletingId.set(doc.id);
      this.api.deleteDocument(this.applicationId(), doc.id).subscribe({
        next: () => {
          this.deletingId.set(null);
          this.removed.emit(doc);
          this.notify.success('Pièce supprimée.');
        },
        error: (error: unknown) => {
          this.deletingId.set(null);
          this.notify.error(error);
        },
      });
    });
  }

  private setError(type: DocumentType, message: string | null): void {
    this.errors.update((e) => ({ ...e, [type]: message ?? undefined }));
  }

  private clearUpload(type: DocumentType): void {
    this.uploads.update((u) => {
      const next = { ...u };
      delete next[type];
      return next;
    });
  }
}
