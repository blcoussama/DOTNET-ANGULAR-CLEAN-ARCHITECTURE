import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE, toHttpParams } from '../http/api.config';
import {
  CreditApplication,
  Page,
  ReviewApplicationDetail,
  ReviewApplicationDetailRaw,
  ReviewApplicationSummary,
  ReviewQuery,
} from '../models/api.models';
import { normalizeReviewDetail } from './normalizers';

export type DecisionAction = 'approve' | 'reject' | 'request-info';

/** Instruction des dossiers (analyste / admin). */
@Injectable({ providedIn: 'root' })
export class ReviewApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/review/applications`;

  list(query: ReviewQuery): Observable<Page<ReviewApplicationSummary>> {
    const params = toHttpParams(
      {
        page: query.page,
        pageSize: query.pageSize,
        status: query.status,
        riskGrade: query.riskGrade,
        productType: query.productType,
        assignedToMe: query.assignedToMe,
        search: query.search,
        sortBy: query.sortBy,
        sortDir: query.sortBy ? (query.sortDir ?? 'desc') : null,
      },
      { dropFalse: true },
    );
    return this.http.get<Page<ReviewApplicationSummary>>(this.base, { params });
  }

  get(id: string): Observable<ReviewApplicationDetail> {
    return this.http
      .get<ReviewApplicationDetailRaw>(`${this.base}/${encodeURIComponent(id)}`)
      .pipe(map((raw) => normalizeReviewDetail(raw)));
  }

  assign(id: string): Observable<CreditApplication> {
    return this.http.post<CreditApplication>(`${this.base}/${encodeURIComponent(id)}/assign`, {});
  }

  decide(id: string, action: DecisionAction, comment: string): Observable<CreditApplication> {
    return this.http.post<CreditApplication>(`${this.base}/${encodeURIComponent(id)}/${action}`, {
      comment: comment.trim(),
    });
  }
}
