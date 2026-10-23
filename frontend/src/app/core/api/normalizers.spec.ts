import { reviewDetail } from '../../../testing/fixtures';
import { DashboardStatsRaw } from '../models/api.models';
import {
  normalizeCounts,
  normalizeDashboardStats,
  normalizeReviewDetail,
  normalizeScoringBreakdown,
  normalizeVolumeByProduct,
} from './normalizers';

describe('normalizeScoringBreakdown', () => {
  it('accepte un objet { critère: points } et ajoute libellés et maxima', () => {
    const result = normalizeScoringBreakdown({
      debtRatioPoints: 25,
      employmentPoints: 22,
      seniority: 10,
      amountToIncome: 12,
      total: 69,
    });
    expect(result.map((c) => [c.key, c.points, c.maxPoints])).toEqual([
      ['debtRatio', 25, 40],
      ['employment', 22, 25],
      ['seniority', 10, 15],
      ['amountToIncome', 12, 20],
    ]);
    expect(result[0].label).toBe("Taux d'endettement");
  });

  it('accepte un tableau de critères avec maxima et détails', () => {
    const result = normalizeScoringBreakdown([
      { criterion: 'SENIORITY', points: 5, maxPoints: 15, detail: '1 an' },
      { criterion: 'DEBT_RATIO', points: 40, maxPoints: 40 },
    ]);
    expect(result.map((c) => c.key)).toEqual(['debtRatio', 'seniority']);
    expect(result[1].detail).toBe('1 an');
  });

  it('accepte un objet enveloppant { criteria: [...] } et des valeurs objets', () => {
    expect(
      normalizeScoringBreakdown({ criteria: [{ key: 'employment', score: 12 }] })[0],
    ).toMatchObject({ key: 'employment', points: 12, maxPoints: 25 });
    expect(normalizeScoringBreakdown({ employment: { points: 18, max: 25 } })[0].points).toBe(18);
  });

  it('conserve un critère inconnu et ignore les entrées invalides', () => {
    const result = normalizeScoringBreakdown({ bonusFidelite: 3, label: 'x', debtRatio: 'n/a' });
    expect(result).toEqual([
      { key: 'bonusFidelite', label: 'bonusFidelite', points: 3, maxPoints: 3, detail: null },
    ]);
    expect(normalizeScoringBreakdown(null)).toEqual([]);
  });
});

describe('normalisation du tableau de bord', () => {
  it('normalise les répartitions objet ou tableau', () => {
    expect(normalizeCounts({ A: 2, b: 1 }, ['A', 'B', 'C'] as const, ['grade'])).toEqual({
      A: 2,
      B: 1,
      C: 0,
    });
    expect(
      normalizeCounts([{ grade: 'C', count: 4 }], ['A', 'B', 'C'] as const, ['grade']),
    ).toEqual({ A: 0, B: 0, C: 4 });
  });

  it('normalise le volume par produit', () => {
    const volumes = normalizeVolumeByProduct([
      { productType: 'AUTO', count: 3, totalAmount: 450000 },
    ]);
    expect(volumes.find((v) => v.productType === 'AUTO')).toEqual({
      productType: 'AUTO',
      count: 3,
      totalAmount: 450000,
    });
    expect(volumes).toHaveLength(3);
    expect(
      normalizeVolumeByProduct({ IMMOBILIER: { count: 1, amount: 900000 } })[2].totalAmount,
    ).toBe(900000);
  });

  it('ramène un taux d’approbation en pourcentage à un ratio et calcule le total', () => {
    const raw: DashboardStatsRaw = {
      countsByStatus: { SUBMITTED: 4, APPROVED: 3, REJECTED: 1 },
      approvalRate: 75,
      averageAmount: 120000,
      totalApprovedAmount: 360000,
      volumeByProduct: [],
      averageProcessingDays: 2.5,
      monthlyTrend: [{ month: '2026-09', submitted: 5, approved: 2, rejected: 1 }],
      gradeDistribution: [{ grade: 'A', count: 2 }],
    };
    const stats = normalizeDashboardStats(raw);
    expect(stats.approvalRate).toBe(0.75);
    expect(stats.totalApplications).toBe(8);
    expect(stats.countsByStatus.DRAFT).toBe(0);
    expect(stats.gradeDistribution).toEqual({ A: 2, B: 0, C: 0, D: 0, E: 0 });
  });
});

describe('normalizeReviewDetail', () => {
  it('compose le nom du client et normalise le score', () => {
    const detail = normalizeReviewDetail(reviewDetail());
    expect(detail.clientName).toBe('Salma Bennani');
    expect(detail.clientEmail).toBe('client@test.ma');
    expect(detail.scoringBreakdown).toHaveLength(4);
  });

  it('utilise clientName s’il est fourni et tolère l’absence de profil', () => {
    const detail = normalizeReviewDetail(
      reviewDetail({ clientName: 'K. Idrissi', client: null, clientProfile: null }),
    );
    expect(detail.clientName).toBe('K. Idrissi');
    expect(detail.clientProfile).toBeNull();
  });
});
