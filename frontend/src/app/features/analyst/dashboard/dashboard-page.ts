import { DecimalPipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { DashboardApi } from '../../../core/api/dashboard.api';
import { errorMessage } from '../../../core/http/problem-details';
import { PRODUCT_LABELS, STATUS_LABELS } from '../../../core/i18n/labels';
import { APPLICATION_STATUSES, DashboardStats, RISK_GRADES } from '../../../core/models/api.models';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/components/states';
import { MadPipe } from '../../../shared/pipes/mad.pipe';
import { gradeChart, monthLabel, productChart, statusChart, trendChart } from './dashboard-charts';

@Component({
  selector: 'app-dashboard-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    PercentPipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    BaseChartDirective,
    LoadingState,
    ErrorState,
    EmptyState,
    MadPipe,
  ],
  // Chart.js n'est chargé (et enregistré) qu'avec cette page.
  providers: [provideCharts(withDefaultRegisterables())],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  private readonly api = inject(DashboardApi);

  protected readonly stats = rxResource({ stream: () => this.api.stats() });
  protected readonly data = computed<DashboardStats | null>(() =>
    this.stats.hasValue() ? (this.stats.value() ?? null) : null,
  );
  protected readonly errorMessage = errorMessage;
  protected readonly statuses = APPLICATION_STATUSES;
  protected readonly grades = RISK_GRADES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly productLabels = PRODUCT_LABELS;
  protected readonly monthLabel = monthLabel;

  protected readonly statusConfig = computed(() => {
    const d = this.data();
    return d ? statusChart(d) : null;
  });
  protected readonly trendConfig = computed(() => {
    const d = this.data();
    return d ? trendChart(d) : null;
  });
  protected readonly productConfig = computed(() => {
    const d = this.data();
    return d ? productChart(d) : null;
  });
  protected readonly gradeConfig = computed(() => {
    const d = this.data();
    return d ? gradeChart(d) : null;
  });
  protected readonly pending = computed(() => {
    const d = this.data();
    return d
      ? d.countsByStatus.SUBMITTED + d.countsByStatus.UNDER_REVIEW + d.countsByStatus.INFO_REQUESTED
      : 0;
  });
}
