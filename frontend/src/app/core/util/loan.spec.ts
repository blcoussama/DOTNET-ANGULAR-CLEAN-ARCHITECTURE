import { PRODUCTS } from '../../../testing/fixtures';
import {
  ageAt,
  amountStep,
  clamp,
  defaultLoanValues,
  durationStep,
  exceedsDebtThreshold,
  isWithinBounds,
} from './loan';

const [conso, , immo] = PRODUCTS;

describe('loan utils', () => {
  it('borne une valeur', () => {
    expect(clamp(10, 0, 5)).toBe(5);
    expect(clamp(-1, 0, 5)).toBe(0);
    expect(clamp(Number.NaN, 2, 5)).toBe(2);
  });

  it('adapte les pas des curseurs au produit', () => {
    expect(amountStep(conso)).toBe(1000);
    expect(amountStep(immo)).toBe(10000);
    expect(durationStep(conso)).toBe(1);
    expect(durationStep(immo)).toBe(12);
  });

  it('propose des valeurs par défaut dans les bornes', () => {
    for (const product of PRODUCTS) {
      const { amount, durationMonths } = defaultLoanValues(product);
      expect(isWithinBounds(product, amount, durationMonths)).toBe(true);
    }
  });

  it('valide les bornes et le caractère entier de la durée', () => {
    expect(isWithinBounds(conso, 5000, 6)).toBe(true);
    expect(isWithinBounds(conso, 4999, 6)).toBe(false);
    expect(isWithinBounds(conso, 5000, 85)).toBe(false);
    expect(isWithinBounds(conso, 5000, 12.5)).toBe(false);
    expect(isWithinBounds(conso, null, 12)).toBe(false);
  });

  it('détecte le dépassement du seuil d’endettement', () => {
    const base = { annualRate: 7.5, monthlyPayment: 1000, totalCost: 100 };
    expect(exceedsDebtThreshold({ ...base, withinDebtThreshold: false, debtRatio: 0.3 })).toBe(
      true,
    );
    expect(exceedsDebtThreshold({ ...base, withinDebtThreshold: true, debtRatio: 0.45 })).toBe(
      false,
    );
    expect(exceedsDebtThreshold({ ...base, debtRatio: 0.41 })).toBe(true);
    expect(exceedsDebtThreshold({ ...base })).toBe(false);
    expect(exceedsDebtThreshold(null)).toBe(false);
  });

  it('calcule l’âge révolu', () => {
    const today = new Date(2026, 9, 22);
    expect(ageAt('2008-10-22', today)).toBe(18);
    expect(ageAt('2008-10-23', today)).toBe(17);
    expect(ageAt('pas-une-date', today)).toBeNull();
  });
});
