import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RECOMMENDATION_LABELS } from '../../core/i18n/labels';
import { Recommendation } from '../../core/models/api.models';

const ICONS: Record<Recommendation, string> = {
  FAVORABLE: 'thumb_up',
  A_ETUDIER: 'help_outline',
  DEFAVORABLE: 'thumb_down',
};

@Component({
  selector: 'app-recommendation-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    @if (recommendation(); as r) {
      <span class="cf-badge" [class]="'cf-badge cf-reco-' + r.toLowerCase().replace('_', '-')">
        <mat-icon aria-hidden="true" class="cf-badge-icon">{{ icon() }}</mat-icon>
        Recommandation : {{ label() }}
      </span>
    }
  `,
})
export class RecommendationBadge {
  readonly recommendation = input<Recommendation | null | undefined>(null);
  protected readonly label = computed(() => {
    const r = this.recommendation();
    return r ? RECOMMENDATION_LABELS[r] : '';
  });
  protected readonly icon = computed(() => {
    const r = this.recommendation();
    return r ? ICONS[r] : '';
  });
}
