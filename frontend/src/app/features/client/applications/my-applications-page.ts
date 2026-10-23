import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ApplicationsApi } from '../../../core/api/applications.api';
import { errorMessage } from '../../../core/http/problem-details';
import { STATUS_LABELS } from '../../../core/i18n/labels';
import {
  APPLICATION_STATUSES,
  ApplicationStatus,
  CreditApplication,
} from '../../../core/models/api.models';
import { StatusBadge } from '../../../shared/components/status-badge';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/components/states';
import { LabelPipe } from '../../../shared/pipes/label.pipe';
import { MadPipe } from '../../../shared/pipes/mad.pipe';
import { latestValue } from '../../../shared/util/resource-utils';

@Component({
  selector: 'app-my-applications-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    StatusBadge,
    EmptyState,
    ErrorState,
    LoadingState,
    LabelPipe,
    MadPipe,
  ],
  templateUrl: './my-applications-page.html',
})
export class MyApplicationsPage {
  private readonly api = inject(ApplicationsApi);

  protected readonly statuses = APPLICATION_STATUSES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly columns = [
    'reference',
    'product',
    'amount',
    'duration',
    'status',
    'createdAt',
    'actions',
  ];
  protected readonly errorMessage = errorMessage;

  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly status = signal<ApplicationStatus | null>(null);

  protected readonly applications = rxResource({
    params: () => ({ page: this.page(), pageSize: this.pageSize(), status: this.status() }),
    stream: ({ params }) => this.api.listMine(params),
  });
  protected readonly data = latestValue(this.applications);
  protected readonly rows = computed<CreditApplication[]>(() => this.data()?.items ?? []);
  protected readonly total = computed(() => this.data()?.totalCount ?? 0);

  protected onStatusChange(status: ApplicationStatus | null): void {
    this.status.set(status);
    this.page.set(1);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.page.set(event.pageIndex + 1);
  }
}
