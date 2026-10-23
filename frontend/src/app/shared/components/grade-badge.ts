import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { GRADE_LABELS } from '../../core/i18n/labels';
import { RiskGrade } from '../../core/models/api.models';

/** Pastille de grade de risque (A à E), avec libellé accessible. */
@Component({
  selector: 'app-grade-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (grade(); as g) {
      <span
        class="cf-grade"
        [class]="'cf-grade cf-grade-' + g.toLowerCase()"
        [attr.aria-label]="ariaLabel()"
        [title]="ariaLabel()"
      >
        {{ g }}
        @if (score() !== null && score() !== undefined) {
          <span class="cf-grade-score"> · {{ score() }}</span>
        }
      </span>
    } @else {
      <span class="cf-muted">—</span>
    }
  `,
})
export class GradeBadge {
  readonly grade = input<RiskGrade | null | undefined>(null);
  readonly score = input<number | null | undefined>(null);
  protected readonly ariaLabel = computed(() => {
    const g = this.grade();
    if (!g) return '';
    const score = this.score();
    return `Grade ${GRADE_LABELS[g]}${score !== null && score !== undefined ? `, score ${score} sur 100` : ''}`;
  });
}
