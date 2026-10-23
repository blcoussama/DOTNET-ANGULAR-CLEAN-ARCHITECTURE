import { CreditProduct, SimulationRequest, SimulationResult } from '../models/api.models';
import { DEFAULT_DEBT_THRESHOLD } from '../i18n/labels';

export function clamp(value: number, min: number, max: number): number {
  if (!Number.isFinite(value)) return min;
  return Math.min(Math.max(value, min), max);
}

/** Pas du curseur de montant, adapté à l'ordre de grandeur du produit. */
export function amountStep(product: Pick<CreditProduct, 'minAmount' | 'maxAmount'>): number {
  const span = product.maxAmount - product.minAmount;
  if (span >= 2_000_000) return 10_000;
  if (span >= 200_000) return 1_000;
  return 500;
}

/** Pas du curseur de durée : 12 mois au-delà de 10 ans d'amplitude, sinon 1 mois. */
export function durationStep(
  product: Pick<CreditProduct, 'minDurationMonths' | 'maxDurationMonths'>,
): number {
  return product.maxDurationMonths - product.minDurationMonths >= 120 ? 12 : 1;
}

/** Valeurs de départ raisonnables pour un produit (≈ quart de la plage, arrondi au pas). */
export function defaultLoanValues(product: CreditProduct): {
  amount: number;
  durationMonths: number;
} {
  const aStep = amountStep(product);
  const dStep = durationStep(product);
  const amount =
    product.minAmount + Math.round((product.maxAmount - product.minAmount) / 4 / aStep) * aStep;
  const duration =
    product.minDurationMonths +
    Math.round((product.maxDurationMonths - product.minDurationMonths) / 3 / dStep) * dStep;
  return {
    amount: clamp(amount, product.minAmount, product.maxAmount),
    durationMonths: clamp(duration, product.minDurationMonths, product.maxDurationMonths),
  };
}

export function isWithinBounds(
  product: CreditProduct,
  amount: number | null,
  durationMonths: number | null,
): boolean {
  return (
    amount !== null &&
    durationMonths !== null &&
    Number.isFinite(amount) &&
    Number.isInteger(durationMonths) &&
    amount >= product.minAmount &&
    amount <= product.maxAmount &&
    durationMonths >= product.minDurationMonths &&
    durationMonths <= product.maxDurationMonths
  );
}

export function sameSimulationRequest(
  a: SimulationRequest | null,
  b: SimulationRequest | null,
): boolean {
  if (a === b) return true;
  if (!a || !b) return false;
  return (
    a.productType === b.productType &&
    a.amount === b.amount &&
    a.durationMonths === b.durationMonths &&
    (a.monthlyIncome ?? null) === (b.monthlyIncome ?? null) &&
    (a.monthlyCharges ?? null) === (b.monthlyCharges ?? null)
  );
}

/** Endettement au-dessus du seuil : on se fie au serveur s'il répond, sinon au seuil par défaut. */
export function exceedsDebtThreshold(result: SimulationResult | null | undefined): boolean {
  if (!result) return false;
  if (result.withinDebtThreshold === false) return true;
  if (result.withinDebtThreshold === true) return false;
  return typeof result.debtRatio === 'number' && result.debtRatio > DEFAULT_DEBT_THRESHOLD;
}

/** Âge révolu à une date donnée (pour la validation du profil). */
export function ageAt(dateOfBirth: string, today: Date = new Date()): number | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateOfBirth);
  if (!match) return null;
  const [y, m, d] = [Number(match[1]), Number(match[2]), Number(match[3])];
  let age = today.getFullYear() - y;
  const monthDiff = today.getMonth() + 1 - m;
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < d)) age--;
  return age;
}
