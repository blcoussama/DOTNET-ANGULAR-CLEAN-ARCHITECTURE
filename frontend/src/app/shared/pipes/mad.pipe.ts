import { Pipe, PipeTransform } from '@angular/core';

const formatters = new Map<number, Intl.NumberFormat>();

/** Formate un montant en dirhams marocains selon les conventions fr-FR : « 12 345,50 MAD ». */
export function formatMad(value: number | null | undefined, fractionDigits = 0): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  let formatter = formatters.get(fractionDigits);
  if (!formatter) {
    formatter = new Intl.NumberFormat('fr-FR', {
      style: 'currency',
      currency: 'MAD',
      currencyDisplay: 'code',
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits,
    });
    formatters.set(fractionDigits, formatter);
  }
  // Normalise les espaces insécables fines pour un rendu homogène.
  return formatter.format(value).replace(/\u202f/g, '\u00a0');
}

@Pipe({ name: 'mad' })
export class MadPipe implements PipeTransform {
  transform(value: number | null | undefined, fractionDigits = 0): string {
    return formatMad(value, fractionDigits);
  }
}
