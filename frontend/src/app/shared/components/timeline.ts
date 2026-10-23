import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ROLE_LABELS, STATUS_LABELS } from '../../core/i18n/labels';
import { ApplicationEvent, ApplicationStatus } from '../../core/models/api.models';

const STATUS_ICONS: Record<ApplicationStatus, string> = {
  DRAFT: 'edit_note',
  SUBMITTED: 'send',
  UNDER_REVIEW: 'manage_search',
  INFO_REQUESTED: 'help_outline',
  APPROVED: 'check_circle',
  REJECTED: 'cancel',
  CANCELLED: 'block',
};

/** Historique des événements d'un dossier (le plus récent en premier). Textes rendus par interpolation. */
@Component({
  selector: 'app-timeline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatIconModule],
  template: `
    @if (sorted().length === 0) {
      <p class="cf-muted">Aucun événement pour le moment.</p>
    } @else {
      <ol class="cf-timeline" aria-label="Historique du dossier">
        @for (event of sorted(); track event.id) {
          <li
            class="cf-timeline-item"
            [class]="
              'cf-timeline-item cf-timeline-' + event.toStatus.toLowerCase().replace('_', '-')
            "
          >
            <span class="cf-timeline-dot" aria-hidden="true"
              ><mat-icon>{{ icon(event.toStatus) }}</mat-icon></span
            >
            <div class="cf-timeline-body">
              <p class="cf-timeline-title">
                @if (event.fromStatus) {
                  {{ statusLabel(event.fromStatus) }} → {{ statusLabel(event.toStatus) }}
                } @else {
                  {{ statusLabel(event.toStatus) }}
                }
              </p>
              <p class="cf-timeline-meta">
                <time [attr.datetime]="event.occurredAt">{{
                  event.occurredAt | date: 'dd/MM/yyyy à HH:mm'
                }}</time>
                · {{ event.actorName || roleLabel(event.actorRole) }}
              </p>
              @if (event.comment) {
                <blockquote class="cf-timeline-comment">{{ event.comment }}</blockquote>
              }
            </div>
          </li>
        }
      </ol>
    }
  `,
})
export class Timeline {
  readonly events = input<ApplicationEvent[]>([]);
  protected readonly sorted = computed(() =>
    [...this.events()].sort(
      (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
    ),
  );
  protected statusLabel(status: ApplicationStatus): string {
    return STATUS_LABELS[status] ?? status;
  }
  protected roleLabel(role: ApplicationEvent['actorRole']): string {
    return ROLE_LABELS[role] ?? role ?? '';
  }
  protected icon(status: ApplicationStatus): string {
    return STATUS_ICONS[status] ?? 'radio_button_checked';
  }
}
