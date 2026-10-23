import { PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { exceedsDebtThreshold } from '../../core/util/loan';
import { MadPipe } from '../pipes/mad.pipe';
import { LiveSimulationState } from '../services/live-simulation';

/** Résultat de simulation : mensualité, taux, coût total, endettement (alerte au-delà du seuil). */
@Component({
  selector: 'app-simulation-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MadPipe, PercentPipe, MatIconModule, MatProgressBarModule],
  template: `
    <section
      class="cf-sim"
      aria-labelledby="sim-title"
      [attr.aria-busy]="state().status === 'loading'"
    >
      <h2 id="sim-title" class="cf-visually-hidden">Résultat de la simulation</h2>
      @if (state().status === 'loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Calcul en cours" />
      } @else {
        <div class="cf-sim-bar-placeholder"></div>
      }
      @if (state().status === 'error') {
        <div class="cf-alert cf-alert-danger" role="alert">
          <mat-icon aria-hidden="true">error_outline</mat-icon>
          <p>{{ state().error }}</p>
        </div>
      }
      @if (result(); as r) {
        <div aria-live="polite">
          <p class="cf-sim-label">Mensualité estimée</p>
          <p class="cf-sim-payment" data-testid="monthly-payment">
            {{ r.monthlyPayment | mad: 2 }}
          </p>
          <dl class="cf-dl">
            <dt>Taux annuel</dt>
            <dd>{{ r.annualRate / 100 | percent: '1.1-2' }}</dd>
            <dt>Coût total du crédit</dt>
            <dd>{{ r.totalCost | mad: 2 }}</dd>
            @if (amount() !== null) {
              <dt>Montant total dû</dt>
              <dd>{{ (amount() ?? 0) + r.totalCost | mad: 2 }}</dd>
            }
            @if (r.debtRatio !== null && r.debtRatio !== undefined) {
              <dt>Taux d'endettement</dt>
              <dd data-testid="debt-ratio">{{ r.debtRatio | percent: '1.0-1' }}</dd>
            }
          </dl>
          @if (overThreshold()) {
            <div class="cf-alert cf-alert-warning" role="alert" data-testid="debt-alert">
              <mat-icon aria-hidden="true">warning_amber</mat-icon>
              <p>
                Votre taux d'endettement dépasserait <strong>40 %</strong>. La demande reste
                possible, mais elle sera probablement jugée défavorable : réduisez le montant ou
                allongez la durée.
              </p>
            </div>
          } @else if (r.debtRatio !== null && r.debtRatio !== undefined) {
            <div class="cf-alert cf-alert-success">
              <mat-icon aria-hidden="true">check_circle</mat-icon>
              <p>Endettement sous le seuil de 40 %.</p>
            </div>
          }
        </div>
      } @else if (state().status === 'idle') {
        <p class="cf-muted">{{ idleMessage() }}</p>
      }
      <p class="cf-sim-disclaimer cf-muted">
        Simulation indicative, sans engagement. Le taux est figé à la soumission de la demande.
      </p>
    </section>
  `,
  styles: `
    .cf-sim {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .cf-sim-bar-placeholder {
      height: 4px;
    }
    .cf-sim-label {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .cf-sim-payment {
      margin: 0 0 12px;
      font: var(--mat-sys-display-small);
      color: var(--mat-sys-primary);
      font-weight: 500;
    }
    .cf-sim-disclaimer {
      margin: 8px 0 0;
      font: var(--mat-sys-body-small);
    }
  `,
})
export class SimulationSummary {
  readonly state = input.required<LiveSimulationState>();
  readonly amount = input<number | null>(null);
  readonly idleMessage = input(
    'Renseignez un montant et une durée valides pour lancer la simulation.',
  );
  protected readonly result = computed(() => this.state().result);
  protected readonly overThreshold = computed(() => exceedsDebtThreshold(this.result()));
}
