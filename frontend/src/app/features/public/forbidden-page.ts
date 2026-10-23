import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { homeUrlFor } from '../../core/util/safe-redirect';

@Component({
  selector: 'app-forbidden-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, RouterLink],
  template: `
    <section class="cf-page cf-centered-page">
      <mat-icon class="cf-hero-icon" aria-hidden="true">lock</mat-icon>
      <h1>Accès refusé</h1>
      <p class="cf-muted">Votre profil ne donne pas accès à cette page.</p>
      <a mat-flat-button [routerLink]="homeLink()">Aller à mon espace</a>
    </section>
  `,
})
export class ForbiddenPage {
  private readonly auth = inject(AuthStore);
  protected readonly homeLink = computed(() => homeUrlFor(this.auth.role()));
}
