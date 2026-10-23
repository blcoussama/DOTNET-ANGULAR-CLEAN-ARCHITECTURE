import { DatePipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ApplicationsApi } from '../../../core/api/applications.api';
import { errorMessage, toApiError } from '../../../core/http/problem-details';
import { ApplicationDetail, ApplicationDocument } from '../../../core/models/api.models';
import { NotificationService } from '../../../core/notify/notification.service';
import { confirmAction } from '../../../shared/components/confirm-dialog';
import { DocumentList } from '../../../shared/components/document-list';
import { StatusBadge } from '../../../shared/components/status-badge';
import { ErrorState, LoadingState } from '../../../shared/components/states';
import { Timeline } from '../../../shared/components/timeline';
import { LabelPipe } from '../../../shared/pipes/label.pipe';
import { MadPipe } from '../../../shared/pipes/mad.pipe';
import { DocumentDownloadService } from '../../../shared/services/document-download.service';
import { DocumentUploader, missingRequiredDocuments } from '../shared/document-uploader';

const CANCELLABLE = ['DRAFT', 'SUBMITTED', 'INFO_REQUESTED'];
const EDITABLE = ['DRAFT', 'INFO_REQUESTED'];

@Component({
  selector: 'app-application-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    PercentPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    StatusBadge,
    LoadingState,
    ErrorState,
    Timeline,
    DocumentList,
    DocumentUploader,
    LabelPipe,
    MadPipe,
  ],
  templateUrl: './application-detail-page.html',
  styleUrl: './application-detail-page.scss',
})
export class ApplicationDetailPage {
  private readonly api = inject(ApplicationsApi);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly downloader = inject(DocumentDownloadService);

  readonly id = input.required<string>();

  protected readonly application = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.api.get(params),
  });
  protected readonly busy = signal(false);
  protected readonly busyDocId = this.downloader.busyId;

  protected readonly detail = computed<ApplicationDetail | null>(() =>
    this.application.hasValue() ? (this.application.value() ?? null) : null,
  );
  protected readonly notFound = computed(() => toApiError(this.application.error()).status === 404);
  protected readonly loadErrorMessage = computed(() => errorMessage(this.application.error()));
  protected readonly canCancel = computed(() => CANCELLABLE.includes(this.detail()?.status ?? ''));
  protected readonly canEdit = computed(() => EDITABLE.includes(this.detail()?.status ?? ''));
  protected readonly isFrozen = computed(() => {
    const d = this.detail();
    return (
      !!d && d.status !== 'DRAFT' && d.monthlyPayment !== null && d.monthlyPayment !== undefined
    );
  });
  protected readonly missingDocuments = computed(() =>
    missingRequiredDocuments(this.detail()?.documents ?? []),
  );
  /** Commentaire de l'analyste qui a demandé des compléments (dernier événement INFO_REQUESTED). */
  protected readonly infoRequest = computed(() => {
    const d = this.detail();
    if (!d) return null;
    const events = [...d.events]
      .filter((e) => e.toStatus === 'INFO_REQUESTED')
      .sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime());
    return events[0]?.comment ?? d.decisionComment ?? null;
  });

  protected onUploaded(doc: ApplicationDocument): void {
    this.application.update((d) => (d ? { ...d, documents: [...d.documents, doc] } : d));
  }

  protected onRemoved(doc: ApplicationDocument): void {
    this.application.update((d) =>
      d ? { ...d, documents: d.documents.filter((x) => x.id !== doc.id) } : d,
    );
  }

  protected download(doc: ApplicationDocument): void {
    this.downloader.download(this.id(), doc).subscribe({ error: () => undefined });
  }

  protected resubmit(): void {
    const d = this.detail();
    if (!d || this.busy()) return;
    confirmAction(this.dialog, {
      title: 'Renvoyer la demande ?',
      message: 'Votre dossier complété sera transmis à nouveau à l’analyste.',
      confirmLabel: 'Renvoyer',
    }).subscribe((ok) => {
      if (!ok) return;
      this.busy.set(true);
      this.api.submit(d.id).subscribe({
        next: () => {
          this.busy.set(false);
          this.notify.success('Demande renvoyée à l’analyste.');
          this.application.reload();
        },
        error: (error: unknown) => {
          this.busy.set(false);
          this.notify.error(error);
        },
      });
    });
  }

  protected cancel(): void {
    const d = this.detail();
    if (!d || this.busy()) return;
    confirmAction(this.dialog, {
      title: 'Annuler la demande ?',
      message: `La demande ${d.reference} sera définitivement annulée. Cette action est irréversible.`,
      confirmLabel: 'Annuler la demande',
      cancelLabel: 'Conserver',
      destructive: true,
    }).subscribe((ok) => {
      if (!ok) return;
      this.busy.set(true);
      this.api.cancel(d.id).subscribe({
        next: () => {
          this.busy.set(false);
          this.notify.success('Demande annulée.');
          this.application.reload();
        },
        error: (error: unknown) => {
          this.busy.set(false);
          this.notify.error(error);
          if (toApiError(error).status === 409) this.application.reload();
        },
      });
    });
  }
}
