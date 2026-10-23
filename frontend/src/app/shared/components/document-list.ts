import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApplicationDocument } from '../../core/models/api.models';
import { FileSizePipe } from '../pipes/file-size.pipe';
import { LabelPipe } from '../pipes/label.pipe';

/** Liste des pièces justificatives, téléchargeables (et supprimables si autorisé). */
@Component({
  selector: 'app-document-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    FileSizePipe,
    LabelPipe,
  ],
  template: `
    @if (documents().length === 0) {
      <p class="cf-muted">{{ emptyMessage() }}</p>
    } @else {
      <ul class="cf-doc-list" aria-label="Pièces justificatives">
        @for (doc of documents(); track doc.id) {
          <li class="cf-doc-item">
            <mat-icon aria-hidden="true" class="cf-doc-icon">{{
              doc.contentType === 'application/pdf' ? 'picture_as_pdf' : 'image'
            }}</mat-icon>
            <div class="cf-doc-text">
              <span class="cf-doc-type">{{ doc.type | label: 'document' }}</span>
              <span class="cf-doc-meta"
                >{{ doc.fileName }} · {{ doc.sizeBytes | fileSize }} ·
                {{ doc.uploadedAt | date: 'dd/MM/yyyy' }}</span
              >
            </div>
            <span class="cf-spacer"></span>
            @if (busyId() === doc.id) {
              <mat-progress-spinner
                mode="indeterminate"
                diameter="24"
                aria-label="Opération en cours"
              />
            } @else {
              <button
                mat-icon-button
                type="button"
                (click)="download.emit(doc)"
                [attr.aria-label]="'Télécharger ' + doc.fileName"
                matTooltip="Télécharger"
              >
                <mat-icon>download</mat-icon>
              </button>
              @if (deletable()) {
                <button
                  mat-icon-button
                  type="button"
                  (click)="remove.emit(doc)"
                  [attr.aria-label]="'Supprimer ' + doc.fileName"
                  matTooltip="Supprimer"
                >
                  <mat-icon>delete</mat-icon>
                </button>
              }
            }
          </li>
        }
      </ul>
    }
  `,
})
export class DocumentList {
  readonly documents = input<ApplicationDocument[]>([]);
  readonly deletable = input(false);
  readonly busyId = input<string | null>(null);
  readonly emptyMessage = input('Aucune pièce déposée.');
  readonly download = output<ApplicationDocument>();
  readonly remove = output<ApplicationDocument>();
}
