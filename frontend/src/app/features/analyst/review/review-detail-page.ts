import { DatePipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { DecisionAction, ReviewApi } from '../../../core/api/review.api';
import { AuthStore } from '../../../core/auth/auth.store';
import { errorMessage, toApiError } from '../../../core/http/problem-details';
import { DEFAULT_DEBT_THRESHOLD } from '../../../core/i18n/labels';
import {
  ApplicationDocument,
  CreditApplication,
  ReviewApplicationDetail,
} from '../../../core/models/api.models';
import { NotificationService } from '../../../core/notify/notification.service';
import { ageAt } from '../../../core/util/loan';
import { confirmAction } from '../../../shared/components/confirm-dialog';
import { DocumentList } from '../../../shared/components/document-list';
import { StatusBadge } from '../../../shared/components/status-badge';
import { ErrorState, LoadingState } from '../../../shared/components/states';
import { Timeline } from '../../../shared/components/timeline';
import { LabelPipe } from '../../../shared/pipes/label.pipe';
import { MadPipe } from '../../../shared/pipes/mad.pipe';
import { DocumentDownloadService } from '../../../shared/services/document-download.service';
import { DecisionDialog, DecisionDialogData } from './decision-dialog';
import { ScoreBreakdown } from './score-breakdown';

const SUCCESS_MESSAGES: Record<DecisionAction, string> = {
  approve: 'Demande approuvée.',
  reject: 'Demande refusée.',
  'request-info': 'Demande de compléments envoyée au client.',
};

@Component({
  selector: 'app-review-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    PercentPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    StatusBadge,
    LoadingState,
    ErrorState,
    Timeline,
    DocumentList,
    ScoreBreakdown,
    LabelPipe,
    MadPipe,
  ],
  templateUrl: './review-detail-page.html',
  styleUrl: './review-detail-page.scss',
})
export class ReviewDetailPage {
  private readonly api = inject(ReviewApi);
  private readonly auth = inject(AuthStore);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly downloader = inject(DocumentDownloadService);

  readonly id = input.required<string>();

  protected readonly dossier = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.api.get(params),
  });
  protected readonly busy = signal(false);
  protected readonly busyDocId = this.downloader.busyId;
  protected readonly threshold = DEFAULT_DEBT_THRESHOLD;

  protected readonly detail = computed<ReviewApplicationDetail | null>(() =>
    this.dossier.hasValue() ? (this.dossier.value() ?? null) : null,
  );
  protected readonly notFound = computed(() => toApiError(this.dossier.error()).status === 404);
  protected readonly loadErrorMessage = computed(() => errorMessage(this.dossier.error()));
  protected readonly isAdmin = computed(() => this.auth.hasRole('Admin'));
  protected readonly assignedToMe = computed(() => {
    const d = this.detail();
    return !!d?.assignedAnalystId && d.assignedAnalystId === this.auth.user()?.id;
  });
  protected readonly canAssign = computed(() => this.detail()?.status === 'SUBMITTED');
  /** Seul l'analyste assigné (ou un admin) décide, sur un dossier en cours d'étude. */
  protected readonly canDecide = computed(
    () => this.detail()?.status === 'UNDER_REVIEW' && (this.assignedToMe() || this.isAdmin()),
  );
  protected readonly clientAge = computed(() => {
    const dob = this.detail()?.clientProfile?.dateOfBirth;
    return dob ? ageAt(dob) : null;
  });
  protected readonly existingDebtRatio = computed(() => {
    const p = this.detail()?.clientProfile;
    return p?.monthlyIncome && p.monthlyIncome > 0 && p.monthlyCharges !== null
      ? p.monthlyCharges / p.monthlyIncome
      : null;
  });
  protected readonly debtOverThreshold = computed(
    () => (this.detail()?.debtRatio ?? 0) > this.threshold,
  );
  protected readonly debtGauge = computed(() =>
    Math.min(100, Math.max(0, (this.detail()?.debtRatio ?? 0) * 100)),
  );

  protected download(doc: ApplicationDocument): void {
    this.downloader.download(this.id(), doc).subscribe({ error: () => undefined });
  }

  protected assign(): void {
    const d = this.detail();
    if (!d || this.busy()) return;
    confirmAction(this.dialog, {
      title: 'Prendre en charge le dossier ?',
      message: `Le dossier ${d.reference} vous sera assigné et passera « en cours d'étude ».`,
      confirmLabel: 'M’assigner le dossier',
    }).subscribe((ok) => {
      if (ok)
        this.run(this.api.assign(d.id), 'Dossier assigné : il est maintenant en cours d’étude.');
    });
  }

  protected decide(action: DecisionAction): void {
    const d = this.detail();
    if (!d || this.busy()) return;
    this.dialog
      .open<DecisionDialog, DecisionDialogData, string>(DecisionDialog, {
        data: { action, reference: d.reference },
        width: '560px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((comment) => {
        if (comment) this.run(this.api.decide(d.id, action, comment), SUCCESS_MESSAGES[action]);
      });
  }

  private run(request$: Observable<CreditApplication>, successMessage: string): void {
    this.busy.set(true);
    request$.subscribe({
      next: () => {
        this.busy.set(false);
        this.notify.success(successMessage);
        this.dossier.reload();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.notify.error(error);
        // 409 : le dossier a changé entre-temps (transition invalide ou concurrence) ⇒ on recharge.
        if (toApiError(error).status === 409) this.dossier.reload();
      },
    });
  }
}
