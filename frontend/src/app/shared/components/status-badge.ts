import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { STATUS_LABELS } from '../../core/i18n/labels';
import { ApplicationStatus } from '../../core/models/api.models';

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="cf-badge" [class]="cssClass()">{{ label() }}</span>`,
})
export class StatusBadge {
  readonly status = input.required<ApplicationStatus>();
  protected readonly label = computed(() => STATUS_LABELS[this.status()] ?? this.status());
  protected readonly cssClass = computed(
    () => `cf-badge cf-status-${this.status().toLowerCase().replace(/_/g, '-')}`,
  );
}
