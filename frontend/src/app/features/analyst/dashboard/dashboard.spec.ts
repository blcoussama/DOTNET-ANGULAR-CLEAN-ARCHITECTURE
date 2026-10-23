import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  provideTestEnvironment,
  settle,
  signIn,
  testId,
  text,
} from '../../../../testing/test-helpers';
import { normalizeDashboardStats } from '../../../core/api/normalizers';
import { DashboardStatsRaw } from '../../../core/models/api.models';
import { gradeChart, monthLabel, productChart, statusChart, trendChart } from './dashboard-charts';
import { DashboardPage } from './dashboard-page';

const RAW: DashboardStatsRaw = {
  countsByStatus: {
    DRAFT: 2,
    SUBMITTED: 3,
    UNDER_REVIEW: 2,
    INFO_REQUESTED: 1,
    APPROVED: 6,
    REJECTED: 2,
    CANCELLED: 0,
  },
  approvalRate: 0.75,
  averageAmount: 145000,
  totalApprovedAmount: 870000,
  volumeByProduct: [
    { productType: 'CONSOMMATION', count: 9, totalAmount: 600000 },
    { productType: 'AUTO', count: 5, totalAmount: 900000 },
  ],
  averageProcessingDays: 3.25,
  monthlyTrend: [
    { month: '2026-05', submitted: 2, approved: 1, rejected: 0 },
    { month: '2026-06', submitted: 3, approved: 2, rejected: 1 },
  ],
  gradeDistribution: { A: 4, B: 3, C: 2, D: 1, E: 1 },
};

describe('Tableau de bord', () => {
  const stats = normalizeDashboardStats(RAW);

  it('construit le graphique des statuts sans les statuts vides ni les brouillons', () => {
    const config = statusChart(stats);
    expect(config.data.labels).not.toContain('Annulée');
    expect(config.data.labels).not.toContain('Brouillon');
    expect(config.data.datasets[0].data).toEqual([3, 2, 1, 6, 2]);
  });

  it('construit la tendance mensuelle avec des mois en français', () => {
    expect(monthLabel('2026-05')).toBe('mai 2026');
    expect(monthLabel('2026-12-01T00:00:00Z')).toBe('déc. 2026');
    const config = trendChart(stats);
    expect(config.data.labels).toEqual(['mai 2026', 'juin 2026']);
    expect(config.data.datasets.map((d) => d.label)).toEqual([
      'Soumises',
      'Approuvées',
      'Refusées',
    ]);
  });

  it('construit le volume par produit et la répartition des grades', () => {
    expect(productChart(stats).data.datasets[0].data).toEqual([600000, 900000, 0]);
    expect(gradeChart(stats).data.datasets[0].data).toEqual([4, 3, 2, 1, 1]);
  });

  it('affiche les indicateurs clés', async () => {
    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: provideTestEnvironment(),
    });
    const http = TestBed.inject(HttpTestingController);
    signIn('Analyst');
    // jsdom n'a pas de canvas : Chart.js journalise une erreur sans planter.
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const getContext = vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
    const fixture = TestBed.createComponent(DashboardPage);
    await settle(fixture);
    http.expectOne('/api/v1/dashboard/stats').flush(RAW);
    await settle(fixture);
    expect(testId(fixture, 'kpi-total')?.textContent).toBe('14');
    expect(testId(fixture, 'kpi-approval')?.textContent).toContain('75');
    expect(text(fixture)).toContain('Tendance sur 6 mois');
    expect(fixture.nativeElement.querySelectorAll('canvas').length).toBe(4);
    http.verify();
    consoleError.mockRestore();
    getContext.mockRestore();
  });
});
