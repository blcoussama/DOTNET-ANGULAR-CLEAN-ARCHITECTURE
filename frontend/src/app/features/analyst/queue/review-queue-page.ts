import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';
import { ReviewApi } from '../../../core/api/review.api';
import { AuthStore } from '../../../core/auth/auth.store';
import { errorMessage } from '../../../core/http/problem-details';
import { PRODUCT_LABELS, STATUS_LABELS } from '../../../core/i18n/labels';
import {
  APPLICATION_STATUSES,
  ApplicationStatus,
  PRODUCT_TYPES,
  ProductType,
  RISK_GRADES,
  ReviewQuery,
  ReviewSortField,
  RiskGrade,
  SortDirection,
} from '../../../core/models/api.models';
import { GradeBadge } from '../../../shared/components/grade-badge';
import { StatusBadge } from '../../../shared/components/status-badge';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/components/states';
import { LabelPipe } from '../../../shared/pipes/label.pipe';
import { MadPipe } from '../../../shared/pipes/mad.pipe';
import { latestValue } from '../../../shared/util/resource-utils';

const SORT_FIELDS: ReviewSortField[] = ['submittedAt', 'amount', 'riskScore'];
const QUEUE_STATUSES = APPLICATION_STATUSES.filter((s) => s !== 'DRAFT');
const DEFAULT_QUERY: ReviewQuery = {
  page: 1,
  pageSize: 20,
  status: null,
  riskGrade: null,
  productType: null,
  assignedToMe: false,
  search: null,
  sortBy: 'submittedAt',
  sortDir: 'asc',
};

/** Lit l'état de la file depuis l'URL (filtres partageables, conservés au retour arrière). */
export function queryFromParams(params: Record<string, string | undefined>): ReviewQuery {
  const pick = <T extends string>(value: string | undefined, allowed: readonly T[]): T | null =>
    allowed.find((a) => a === value) ?? null;
  const page = Number(params['page']);
  const pageSize = Number(params['pageSize']);
  return {
    page: Number.isInteger(page) && page > 0 ? page : DEFAULT_QUERY.page,
    pageSize: [10, 20, 50, 100].includes(pageSize) ? pageSize : DEFAULT_QUERY.pageSize,
    status: pick<ApplicationStatus>(params['status'], QUEUE_STATUSES),
    riskGrade: pick<RiskGrade>(params['riskGrade'], RISK_GRADES),
    productType: pick<ProductType>(params['productType'], PRODUCT_TYPES),
    assignedToMe: params['assignedToMe'] === 'true',
    search: params['search']?.slice(0, 100) || null,
    sortBy: pick<ReviewSortField>(params['sortBy'], SORT_FIELDS) ?? DEFAULT_QUERY.sortBy,
    sortDir: pick<SortDirection>(params['sortDir'], ['asc', 'desc']) ?? DEFAULT_QUERY.sortDir,
  };
}

export function queryToParams(query: ReviewQuery): Record<string, string | null> {
  return {
    page: query.page !== DEFAULT_QUERY.page ? String(query.page) : null,
    pageSize: query.pageSize !== DEFAULT_QUERY.pageSize ? String(query.pageSize) : null,
    status: query.status ?? null,
    riskGrade: query.riskGrade ?? null,
    productType: query.productType ?? null,
    assignedToMe: query.assignedToMe ? 'true' : null,
    search: query.search || null,
    sortBy: query.sortBy && query.sortBy !== DEFAULT_QUERY.sortBy ? query.sortBy : null,
    sortDir: query.sortDir && query.sortDir !== DEFAULT_QUERY.sortDir ? query.sortDir : null,
  };
}

@Component({
  selector: 'app-review-queue-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSortModule,
    MatTableModule,
    GradeBadge,
    StatusBadge,
    EmptyState,
    ErrorState,
    LoadingState,
    LabelPipe,
    MadPipe,
  ],
  templateUrl: './review-queue-page.html',
})
export class ReviewQueuePage {
  private readonly api = inject(ReviewApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly auth = inject(AuthStore);

  protected readonly statuses = QUEUE_STATUSES;
  protected readonly grades = RISK_GRADES;
  protected readonly products = PRODUCT_TYPES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly productLabels = PRODUCT_LABELS;
  protected readonly columns = [
    'reference',
    'client',
    'product',
    'amount',
    'submittedAt',
    'status',
    'riskScore',
    'assigned',
  ];
  protected readonly errorMessage = errorMessage;

  protected readonly query = signal<ReviewQuery>(
    queryFromParams(this.route.snapshot.queryParams as Record<string, string | undefined>),
  );
  protected readonly searchControl = new FormControl(this.query().search ?? '', {
    nonNullable: true,
  });

  protected readonly applications = rxResource({
    params: () => this.query(),
    stream: ({ params }) => this.api.list(params),
  });
  protected readonly data = latestValue(this.applications);
  protected readonly rows = computed(() => this.data()?.items ?? []);
  protected readonly total = computed(() => this.data()?.totalCount ?? 0);
  protected readonly hasFilters = computed(() => {
    const q = this.query();
    return !!(q.status || q.riskGrade || q.productType || q.assignedToMe || q.search);
  });

  constructor() {
    this.searchControl.valueChanges
      .pipe(
        map((value) => value.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((search) => this.patch({ search: search || null }));

    // Synchronise l'URL (sans entrée d'historique) avec les filtres.
    effect(() => {
      const queryParams = queryToParams(this.query());
      void this.router.navigate([], { relativeTo: this.route, queryParams, replaceUrl: true });
    });
  }

  protected patch(changes: Partial<ReviewQuery>, resetPage = true): void {
    this.query.update((q) => ({ ...q, ...changes, ...(resetPage ? { page: 1 } : {}) }));
  }

  protected onSort(sort: Sort): void {
    const field = SORT_FIELDS.find((f) => f === sort.active) ?? null;
    if (!sort.direction || !field) {
      this.patch({ sortBy: DEFAULT_QUERY.sortBy, sortDir: DEFAULT_QUERY.sortDir });
    } else {
      this.patch({ sortBy: field, sortDir: sort.direction });
    }
  }

  protected onPage(event: PageEvent): void {
    this.patch({ page: event.pageIndex + 1, pageSize: event.pageSize }, false);
  }

  protected resetFilters(): void {
    this.searchControl.setValue('', { emitEvent: false });
    this.query.set({ ...DEFAULT_QUERY, pageSize: this.query().pageSize });
  }
}
