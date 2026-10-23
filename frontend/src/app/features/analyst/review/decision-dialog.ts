import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { DecisionAction } from '../../../core/api/review.api';

export interface DecisionDialogData {
  action: DecisionAction;
  reference: string;
}

export const COMMENT_MAX_LENGTH = 1000;

export const DECISION_TEXTS: Record<
  DecisionAction,
  { title: string; intro: string; confirm: string; placeholder: string }
> = {
  approve: {
    title: 'Approuver la demande',
    intro: 'Le client sera informé de l’accord. Motivez votre décision.',
    confirm: 'Confirmer l’approbation',
    placeholder: 'Ex. : capacité de remboursement confirmée, endettement maîtrisé…',
  },
  reject: {
    title: 'Refuser la demande',
    intro: 'Le refus est définitif. Le commentaire sera visible par le client.',
    confirm: 'Confirmer le refus',
    placeholder: 'Ex. : taux d’endettement trop élevé au regard des revenus…',
  },
  'request-info': {
    title: 'Demander des compléments',
    intro: 'Le dossier repasse au client, qui pourra déposer des pièces puis le renvoyer.',
    confirm: 'Envoyer la demande',
    placeholder: 'Ex. : merci de fournir vos trois derniers relevés bancaires.',
  },
};

/** Saisie du commentaire obligatoire (≤ 1000) et confirmation explicite de la décision. */
@Component({
  selector: 'app-decision-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ texts.title }} — {{ data.reference }}</h2>
    <form [formGroup]="form" (ngSubmit)="confirm()">
      <mat-dialog-content>
        <p>{{ texts.intro }}</p>
        <mat-form-field appearance="outline" class="cf-field-full">
          <mat-label>Commentaire (obligatoire)</mat-label>
          <textarea
            matInput
            formControlName="comment"
            rows="5"
            [maxlength]="max"
            [placeholder]="texts.placeholder"
            cdkFocusInitial
            required
            data-testid="decision-comment"
          ></textarea>
          <mat-hint align="end">{{ form.controls.comment.value.length }} / {{ max }}</mat-hint>
          @if (
            form.controls.comment.hasError('required') || form.controls.comment.hasError('blank')
          ) {
            <mat-error>Le commentaire est obligatoire.</mat-error>
          } @else if (form.controls.comment.hasError('maxlength')) {
            <mat-error>{{ max }} caractères maximum.</mat-error>
          }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Annuler</button>
        <button
          mat-flat-button
          type="submit"
          data-testid="decision-confirm"
          [class.cf-danger-button]="data.action === 'reject'"
          [class.cf-success-button]="data.action === 'approve'"
        >
          {{ texts.confirm }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
})
export class DecisionDialog {
  protected readonly data = inject<DecisionDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<DecisionDialog, string>>(MatDialogRef);
  protected readonly texts = DECISION_TEXTS[this.data.action];
  protected readonly max = COMMENT_MAX_LENGTH;
  protected readonly form = inject(NonNullableFormBuilder).group({
    comment: [
      '',
      [
        Validators.required,
        Validators.maxLength(COMMENT_MAX_LENGTH),
        (c: { value: string }) => (c.value && !c.value.trim() ? { blank: true } : null),
      ],
    ],
  });

  protected confirm(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.ref.close(this.form.controls.comment.value.trim());
  }
}
