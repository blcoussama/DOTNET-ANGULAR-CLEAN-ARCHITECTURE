import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ProductsApi } from '../../../core/api/products.api';
import { errorMessage } from '../../../core/http/problem-details';
import { CreditProduct } from '../../../core/models/api.models';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/components/states';
import { ProductEditor } from './product-editor';

@Component({
  selector: 'app-products-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProductEditor, LoadingState, ErrorState, EmptyState],
  template: `
    <section class="cf-page cf-page-narrow">
      <header class="cf-page-header">
        <div>
          <h1>Produits de crédit</h1>
          <p class="cf-muted">
            Taux et bornes appliqués aux nouvelles simulations et demandes. Les demandes déjà
            soumises conservent leur taux figé.
          </p>
        </div>
      </header>
      @if (products.error()) {
        <app-error-state
          title="Impossible de charger les produits"
          [message]="errorMessage(products.error())"
          (retry)="products.reload()"
        />
      } @else if (products.isLoading() && !products.hasValue()) {
        <app-loading-state label="Chargement des produits…" />
      } @else if (products.hasValue()) {
        <div class="cf-stack">
          @for (product of products.value(); track product.type) {
            <app-product-editor [product]="product" (saved)="onSaved($event)" />
          } @empty {
            <app-empty-state icon="tune" title="Aucun produit configuré" />
          }
        </div>
      }
    </section>
  `,
})
export class ProductsPage {
  private readonly api = inject(ProductsApi);
  protected readonly products = rxResource({ stream: () => this.api.listAll() });
  protected readonly errorMessage = errorMessage;

  protected onSaved(updated: CreditProduct): void {
    this.products.update((list) => list?.map((p) => (p.type === updated.type ? updated : p)));
  }
}
