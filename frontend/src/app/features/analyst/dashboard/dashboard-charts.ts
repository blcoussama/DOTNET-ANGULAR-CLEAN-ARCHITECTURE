import { ChartConfiguration } from 'chart.js';
import { PRODUCT_LABELS, STATUS_LABELS } from '../../../core/i18n/labels';
import {
  APPLICATION_STATUSES,
  ApplicationStatus,
  DashboardStats,
  RISK_GRADES,
  RiskGrade,
} from '../../../core/models/api.models';

export const STATUS_COLORS: Record<ApplicationStatus, string> = {
  DRAFT: '#90a4ae',
  SUBMITTED: '#1e88e5',
  UNDER_REVIEW: '#5e35b1',
  INFO_REQUESTED: '#fb8c00',
  APPROVED: '#2e7d32',
  REJECTED: '#c62828',
  CANCELLED: '#bdbdbd',
};

export const GRADE_COLORS: Record<RiskGrade, string> = {
  A: '#2e7d32',
  B: '#7cb342',
  C: '#fbc02d',
  D: '#f4511e',
  E: '#c62828',
};

const MONTHS = [
  'janv.',
  'févr.',
  'mars',
  'avr.',
  'mai',
  'juin',
  'juil.',
  'août',
  'sept.',
  'oct.',
  'nov.',
  'déc.',
];

/** « 2026-05 » ou « 2026-05-01T00:00:00Z » ⇒ « mai 2026 ». */
export function monthLabel(month: string): string {
  const match = /^(\d{4})-(\d{2})/.exec(month);
  if (!match) return month;
  const index = Number(match[2]) - 1;
  return index >= 0 && index < 12 ? `${MONTHS[index]} ${match[1]}` : month;
}

const legendBottom = { legend: { position: 'bottom' as const } };

export function statusChart(stats: DashboardStats): ChartConfiguration<'doughnut'> {
  // Les brouillons ne sont pas encore transmis à la banque : exclus, comme dans les indicateurs.
  const statuses = APPLICATION_STATUSES.filter((s) => s !== 'DRAFT' && stats.countsByStatus[s] > 0);
  return {
    type: 'doughnut',
    data: {
      labels: statuses.map((s) => STATUS_LABELS[s]),
      datasets: [
        {
          data: statuses.map((s) => stats.countsByStatus[s]),
          backgroundColor: statuses.map((s) => STATUS_COLORS[s]),
          borderWidth: 1,
        },
      ],
    },
    options: { responsive: true, maintainAspectRatio: false, plugins: legendBottom },
  };
}

export function trendChart(stats: DashboardStats): ChartConfiguration<'line'> {
  const labels = stats.monthlyTrend.map((p) => monthLabel(p.month));
  const series = (label: string, color: string, values: number[]) => ({
    label,
    data: values,
    borderColor: color,
    backgroundColor: color,
    // Segments droits : un lissage inventerait des valeurs entre deux mois (comptages entiers).
    tension: 0,
    pointRadius: 3,
  });
  return {
    type: 'line',
    data: {
      labels,
      datasets: [
        series(
          'Soumises',
          STATUS_COLORS.SUBMITTED,
          stats.monthlyTrend.map((p) => p.submitted),
        ),
        series(
          'Approuvées',
          STATUS_COLORS.APPROVED,
          stats.monthlyTrend.map((p) => p.approved),
        ),
        series(
          'Refusées',
          STATUS_COLORS.REJECTED,
          stats.monthlyTrend.map((p) => p.rejected),
        ),
      ],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: legendBottom,
      scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
    },
  };
}

export function productChart(stats: DashboardStats): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: stats.volumeByProduct.map((v) => PRODUCT_LABELS[v.productType]),
      datasets: [
        {
          label: 'Montant demandé (MAD)',
          data: stats.volumeByProduct.map((v) => v.totalAmount),
          backgroundColor: ['#1565c0', '#00838f', '#6a1b9a'],
          yAxisID: 'y',
        },
        {
          label: 'Nombre de demandes',
          data: stats.volumeByProduct.map((v) => v.count),
          backgroundColor: '#90caf9',
          yAxisID: 'y1',
        },
      ],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: legendBottom,
      scales: {
        y: {
          beginAtZero: true,
          position: 'left',
          ticks: { callback: (v) => Number(v).toLocaleString('fr-FR') },
        },
        y1: {
          beginAtZero: true,
          position: 'right',
          grid: { drawOnChartArea: false },
          ticks: { precision: 0 },
        },
      },
    },
  };
}

export function gradeChart(stats: DashboardStats): ChartConfiguration<'bar'> {
  return {
    type: 'bar',
    data: {
      labels: RISK_GRADES.map((g) => `Grade ${g}`),
      datasets: [
        {
          label: 'Demandes',
          data: RISK_GRADES.map((g) => stats.gradeDistribution[g]),
          backgroundColor: RISK_GRADES.map((g) => GRADE_COLORS[g]),
        },
      ],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
    },
  };
}
