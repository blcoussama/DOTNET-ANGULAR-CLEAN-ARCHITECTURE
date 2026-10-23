import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Recommendation, RiskGrade, ScoreCriterion } from '../../../core/models/api.models';
import { GradeBadge } from '../../../shared/components/grade-badge';
import { RecommendationBadge } from '../../../shared/components/recommendation-badge';

/** Score /100, grade, recommandation et décomposition par critère (barres accessibles). */
@Component({
  selector: 'app-score-breakdown',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, GradeBadge, RecommendationBadge],
  template: `
    <div class="cf-score-head">
      <div
        class="cf-score-total"
        [attr.aria-label]="'Score de risque : ' + (score() ?? '—') + ' sur 100'"
      >
        <span class="cf-score-value" data-testid="score-value">{{ score() ?? '—' }}</span
        ><span class="cf-muted">/100</span>
      </div>
      <app-grade-badge [grade]="grade()" />
      <app-recommendation-badge [recommendation]="recommendation()" />
    </div>

    @if (criteria().length) {
      <ul class="cf-criteria" aria-label="Décomposition du score">
        @for (c of criteria(); track c.key) {
          <li class="cf-criterion" data-testid="criterion">
            <div class="cf-row">
              <span class="cf-criterion-label">{{ c.label }}</span>
              <span class="cf-spacer"></span>
              <span class="cf-criterion-points">{{ c.points }} / {{ c.maxPoints }} pts</span>
            </div>
            <div
              class="cf-bar"
              role="meter"
              [attr.aria-label]="c.label"
              aria-valuemin="0"
              [attr.aria-valuemax]="c.maxPoints"
              [attr.aria-valuenow]="c.points"
              [attr.aria-valuetext]="c.points + ' points sur ' + c.maxPoints"
            >
              <span
                class="cf-bar-fill"
                [class]="'cf-bar-fill ' + levelClass(c)"
                [style.width.%]="percent(c)"
              ></span>
            </div>
            @if (c.detail) {
              <p class="cf-muted cf-criterion-detail">{{ c.detail }}</p>
            }
          </li>
        }
      </ul>
      <p class="cf-muted cf-score-sum">Total des critères : {{ sum() }} / {{ maxSum() }} points</p>
    } @else {
      <p class="cf-muted">Décomposition du score indisponible.</p>
    }
    <p class="cf-score-note">
      <mat-icon aria-hidden="true">info</mat-icon>
      Le score est une aide à l'analyse ; il ne constitue pas une décision.
    </p>
  `,
  styles: `
    .cf-score-head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }
    .cf-score-value {
      font: var(--mat-sys-display-small);
      font-weight: 500;
      color: var(--mat-sys-primary);
    }
    .cf-criteria {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 12px;
    }
    .cf-criterion-label {
      font-weight: 500;
    }
    .cf-criterion-points {
      font-variant-numeric: tabular-nums;
    }
    .cf-criterion-detail {
      margin: 4px 0 0;
      font: var(--mat-sys-body-small);
    }
    .cf-bar {
      height: 10px;
      margin-top: 6px;
      border-radius: 5px;
      background: var(--mat-sys-surface-container-highest);
      overflow: hidden;
    }
    .cf-bar-fill {
      display: block;
      height: 100%;
      border-radius: 5px;
    }
    .cf-level-high {
      background: #2e7d32;
    }
    .cf-level-mid {
      background: #f9a825;
    }
    .cf-level-low {
      background: #c62828;
    }
    .cf-score-sum {
      margin: 12px 0 0;
    }
    .cf-score-note {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 12px 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ScoreBreakdown {
  readonly score = input<number | null | undefined>(null);
  readonly grade = input<RiskGrade | null | undefined>(null);
  readonly recommendation = input<Recommendation | null | undefined>(null);
  readonly criteria = input<ScoreCriterion[]>([]);

  protected readonly sum = computed(() =>
    this.criteria().reduce((total, c) => total + c.points, 0),
  );
  protected readonly maxSum = computed(() =>
    this.criteria().reduce((total, c) => total + c.maxPoints, 0),
  );

  protected percent(c: ScoreCriterion): number {
    return c.maxPoints > 0 ? Math.max(0, Math.min(100, (c.points / c.maxPoints) * 100)) : 0;
  }

  protected levelClass(c: ScoreCriterion): string {
    const p = this.percent(c);
    return p >= 60 ? 'cf-level-high' : p >= 30 ? 'cf-level-mid' : 'cf-level-low';
  }
}
