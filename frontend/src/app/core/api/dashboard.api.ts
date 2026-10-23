import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE } from '../http/api.config';
import { DashboardStats, DashboardStatsRaw } from '../models/api.models';
import { normalizeDashboardStats } from './normalizers';

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  stats(): Observable<DashboardStats> {
    return this.http
      .get<DashboardStatsRaw>(`${API_BASE}/dashboard/stats`)
      .pipe(map((raw) => normalizeDashboardStats(raw)));
  }
}
