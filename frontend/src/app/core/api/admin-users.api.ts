import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, toHttpParams } from '../http/api.config';
import {
  AdminUser,
  AdminUsersQuery,
  CreateStaffUserRequest,
  Page,
  UpdateUserRequest,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AdminUsersApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/admin/users`;

  list(query: AdminUsersQuery): Observable<Page<AdminUser>> {
    const params = toHttpParams({
      page: query.page,
      pageSize: query.pageSize,
      role: query.role,
      search: query.search,
    });
    return this.http.get<Page<AdminUser>>(this.base, { params });
  }

  create(body: CreateStaffUserRequest): Observable<AdminUser> {
    return this.http.post<AdminUser>(this.base, body);
  }

  update(id: string, body: UpdateUserRequest): Observable<AdminUser> {
    return this.http.patch<AdminUser>(`${this.base}/${encodeURIComponent(id)}`, body);
  }
}
