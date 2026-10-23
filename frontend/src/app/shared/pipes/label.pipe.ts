import { Pipe, PipeTransform } from '@angular/core';
import {
  DOCUMENT_LABELS,
  EMPLOYMENT_LABELS,
  PRODUCT_LABELS,
  RECOMMENDATION_LABELS,
  ROLE_LABELS,
  STATUS_LABELS,
} from '../../core/i18n/labels';

const DICTIONARIES = {
  status: STATUS_LABELS,
  product: PRODUCT_LABELS,
  employment: EMPLOYMENT_LABELS,
  document: DOCUMENT_LABELS,
  recommendation: RECOMMENDATION_LABELS,
  role: ROLE_LABELS,
} as const;

export type LabelDictionary = keyof typeof DICTIONARIES;

/** Libellé français d'une valeur d'énumération de l'API : `{{ status | label: 'status' }}`. */
@Pipe({ name: 'label' })
export class LabelPipe implements PipeTransform {
  transform(value: string | null | undefined, dictionary: LabelDictionary): string {
    if (!value) return '—';
    const labels = DICTIONARIES[dictionary] as Record<string, string>;
    return labels[value] ?? value;
  }
}
