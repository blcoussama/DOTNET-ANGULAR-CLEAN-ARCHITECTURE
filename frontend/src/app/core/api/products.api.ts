import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE } from '../http/api.config';
import {
  CreditProduct,
  CreditProductUpdate,
  PRODUCT_TYPES,
  ProductType,
} from '../models/api.models';

const byProductOrder = (a: CreditProduct, b: CreditProduct) =>
  PRODUCT_TYPES.indexOf(a.type) - PRODUCT_TYPES.indexOf(b.type);

@Injectable({ providedIn: 'root' })
export class ProductsApi {
  private readonly http = inject(HttpClient);

  /** Produits actifs (public). */
  listActive(): Observable<CreditProduct[]> {
    return this.http
      .get<CreditProduct[]>(`${API_BASE}/products`)
      .pipe(
        map((products) => [...products].filter((p) => p.isActive !== false).sort(byProductOrder)),
      );
  }

  /** Tous les produits, y compris inactifs (admin). */
  listAll(): Observable<CreditProduct[]> {
    return this.http
      .get<CreditProduct[]>(`${API_BASE}/admin/products`)
      .pipe(map((products) => [...products].sort(byProductOrder)));
  }

  update(type: ProductType, body: CreditProductUpdate): Observable<CreditProduct> {
    return this.http.put<CreditProduct>(
      `${API_BASE}/admin/products/${encodeURIComponent(type)}`,
      body,
    );
  }
}
