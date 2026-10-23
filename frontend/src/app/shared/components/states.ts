import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-loading-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="cf-state" role="status" aria-live="polite">
      <mat-progress-spinner mode="indeterminate" diameter="40" aria-hidden="true" />
      <p>{{ label() }}</p>
    </div>
  `,
})
export class LoadingState {
  readonly label = input('Chargement…');
}

@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="cf-state">
      <mat-icon class="cf-state-icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <p class="cf-state-title">{{ title() }}</p>
      @if (message()) {
        <p class="cf-muted">{{ message() }}</p>
      }
      <ng-content />
    </div>
  `,
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly title = input.required<string>();
  readonly message = input<string | null>(null);
}

@Component({
  selector: 'app-error-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="cf-state cf-state-error" role="alert">
      <mat-icon class="cf-state-icon" aria-hidden="true">error_outline</mat-icon>
      <p class="cf-state-title">{{ title() }}</p>
      @if (message()) {
        <p>{{ message() }}</p>
      }
      @if (retryable()) {
        <button mat-stroked-button type="button" (click)="retry.emit()">
          <mat-icon aria-hidden="true">refresh</mat-icon> Réessayer
        </button>
      }
    </div>
  `,
})
export class ErrorState {
  readonly title = input('Le chargement a échoué');
  readonly message = input<string | null>(null);
  readonly retryable = input(true);
  readonly retry = output<void>();
}
