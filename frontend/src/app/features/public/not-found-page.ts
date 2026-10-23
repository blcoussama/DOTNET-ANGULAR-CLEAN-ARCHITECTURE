import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { homeUrlFor } from '../../core/util/safe-redirect';

@Component({
  selector: 'app-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    <section class="cf-page cf-centered-page">
      <mat-icon class="cf-hero-icon" aria-hidden="true">travel_explore</mat-icon>
      <h1>Page introuvable</h1>
      <p class="cf-muted">La page demandée n'existe pas ou a été déplacée.</p>
      <a mat-flat-button [routerLink]="homeLink()">Retour à l'accueil</a>
    </section>
  `,
})
export class NotFoundPage {
  private readonly auth = inject(AuthStore);
  protected readonly homeLink = computed(() => homeUrlFor(this.auth.role()));
}
