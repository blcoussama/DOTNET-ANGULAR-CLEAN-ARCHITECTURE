import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSliderModule } from '@angular/material/slider';
import { CreditProduct } from '../../core/models/api.models';
import { amountStep, durationStep } from '../../core/util/loan';
import { formatMad } from '../pipes/mad.pipe';

/** Curseurs + saisies numériques montant / durée, bornés par le produit choisi. */
@Component({
  selector: 'app-loan-fields',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSliderModule],
  template: `
    <div class="cf-loan-fields">
      <div class="cf-loan-field">
        <mat-form-field class="cf-field-full">
          <mat-label>Montant souhaité (MAD)</mat-label>
          <input
            matInput
            type="number"
            inputmode="numeric"
            [formControl]="amount()"
            [min]="product().minAmount"
            [max]="product().maxAmount"
            [step]="aStep()"
            [attr.aria-describedby]="idPrefix() + '-amount-hint'"
          />
          <mat-hint [id]="idPrefix() + '-amount-hint'"
            >Entre {{ fmt(product().minAmount) }} et {{ fmt(product().maxAmount) }}</mat-hint
          >
          @if (amount().hasError('required')) {
            <mat-error>Le montant est obligatoire.</mat-error>
          } @else if (amount().hasError('min') || amount().hasError('max')) {
            <mat-error
              >Le montant doit être compris entre {{ fmt(product().minAmount) }} et
              {{ fmt(product().maxAmount) }}.</mat-error
            >
          }
        </mat-form-field>
        <mat-slider
          class="cf-slider"
          [min]="product().minAmount"
          [max]="product().maxAmount"
          [step]="aStep()"
          discrete
          [displayWith]="shortMad"
        >
          <input matSliderThumb [formControl]="amount()" aria-label="Curseur du montant" />
        </mat-slider>
      </div>

      <div class="cf-loan-field">
        <mat-form-field class="cf-field-full">
          <mat-label>Durée (mois)</mat-label>
          <input
            matInput
            type="number"
            inputmode="numeric"
            [formControl]="duration()"
            [min]="product().minDurationMonths"
            [max]="product().maxDurationMonths"
            step="1"
            [attr.aria-describedby]="idPrefix() + '-duration-hint'"
          />
          <mat-hint [id]="idPrefix() + '-duration-hint'"
            >Entre {{ product().minDurationMonths }} et {{ product().maxDurationMonths }} mois ({{
              years()
            }})</mat-hint
          >
          @if (duration().hasError('required')) {
            <mat-error>La durée est obligatoire.</mat-error>
          } @else if (duration().invalid) {
            <mat-error
              >La durée doit être un nombre entier de mois entre
              {{ product().minDurationMonths }} et {{ product().maxDurationMonths }}.</mat-error
            >
          }
        </mat-form-field>
        <mat-slider
          class="cf-slider"
          [min]="product().minDurationMonths"
          [max]="product().maxDurationMonths"
          [step]="dStep()"
          discrete
        >
          <input matSliderThumb [formControl]="duration()" aria-label="Curseur de la durée" />
        </mat-slider>
      </div>
    </div>
  `,
  styles: `
    .cf-loan-fields {
      display: grid;
      gap: 8px;
    }
    .cf-loan-field {
      display: flex;
      flex-direction: column;
    }
    .cf-slider {
      width: calc(100% - 16px);
      margin: 0 8px 8px;
    }
  `,
})
export class LoanFields {
  readonly amount = input.required<FormControl<number>>();
  readonly duration = input.required<FormControl<number>>();
  readonly product = input.required<CreditProduct>();
  /** Valeur courante de la durée (signal du parent) pour l'affichage en années. */
  readonly durationValue = input<number | null>(null);
  readonly idPrefix = input('loan');

  protected readonly aStep = computed(() => amountStep(this.product()));
  protected readonly dStep = computed(() => durationStep(this.product()));
  protected readonly years = computed(() => {
    const months = this.durationValue();
    if (!months) return '—';
    const years = months / 12;
    return `${years.toLocaleString('fr-FR', { maximumFractionDigits: 1 })} an${years >= 2 ? 's' : ''}`;
  });

  protected fmt(value: number): string {
    return formatMad(value);
  }

  protected shortMad(value: number): string {
    if (value >= 1_000_000)
      return `${(value / 1_000_000).toLocaleString('fr-FR', { maximumFractionDigits: 1 })} M`;
    if (value >= 1000) return `${Math.round(value / 1000)} k`;
    return String(value);
  }
}
