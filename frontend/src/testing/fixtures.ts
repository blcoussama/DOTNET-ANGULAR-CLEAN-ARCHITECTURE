import {
  ApplicationDetail,
  ApplicationDocument,
  ClientProfile,
  CreditProduct,
  ReviewApplicationDetailRaw,
} from '../app/core/models/api.models';

export const PRODUCTS: CreditProduct[] = [
  {
    type: 'CONSOMMATION',
    annualRate: 7.5,
    minAmount: 5000,
    maxAmount: 300000,
    minDurationMonths: 6,
    maxDurationMonths: 84,
    isActive: true,
  },
  {
    type: 'AUTO',
    annualRate: 6.5,
    minAmount: 20000,
    maxAmount: 600000,
    minDurationMonths: 12,
    maxDurationMonths: 84,
    isActive: true,
  },
  {
    type: 'IMMOBILIER',
    annualRate: 4.9,
    minAmount: 100000,
    maxAmount: 5000000,
    minDurationMonths: 60,
    maxDurationMonths: 300,
    isActive: true,
  },
];

export const COMPLETE_PROFILE: ClientProfile = {
  dateOfBirth: '1990-04-12',
  phone: '+212 612345678',
  city: 'Tanger',
  employmentType: 'CDI',
  employer: 'Banque du Nord',
  monthlyIncome: 15000,
  monthlyCharges: 2000,
  yearsAtCurrentJob: 6,
  isComplete: true,
};

export function doc(
  id: string,
  type: ApplicationDocument['type'],
  applicationId = 'app-1',
): ApplicationDocument {
  return {
    id,
    applicationId,
    type,
    fileName: `${type.toLowerCase()}.pdf`,
    contentType: 'application/pdf',
    sizeBytes: 120_000,
    uploadedAt: '2026-10-01T10:00:00Z',
  };
}

export function application(overrides: Partial<ApplicationDetail> = {}): ApplicationDetail {
  return {
    id: 'app-1',
    reference: 'CF-2026-000042',
    clientId: 'u-client',
    productType: 'CONSOMMATION',
    amount: 80000,
    durationMonths: 48,
    purpose: 'Travaux de rénovation',
    status: 'DRAFT',
    annualRate: null,
    monthlyPayment: null,
    totalCost: null,
    debtRatio: null,
    riskScore: null,
    riskGrade: null,
    recommendation: null,
    assignedAnalystId: null,
    decisionComment: null,
    createdAt: '2026-10-01T09:00:00Z',
    submittedAt: null,
    decidedAt: null,
    documents: [],
    events: [],
    ...overrides,
  };
}

export function reviewDetail(
  overrides: Partial<ReviewApplicationDetailRaw> = {},
): ReviewApplicationDetailRaw {
  return {
    ...application({
      status: 'UNDER_REVIEW',
      annualRate: 7.5,
      monthlyPayment: 1934.4,
      totalCost: 12851.2,
      debtRatio: 0.262,
      riskScore: 82,
      riskGrade: 'A',
      recommendation: 'FAVORABLE',
      submittedAt: '2026-10-02T09:00:00Z',
      assignedAnalystId: 'u-analyst',
      documents: [doc('d1', 'ID_CARD'), doc('d2', 'PAYSLIP')],
      events: [
        {
          id: 'e1',
          applicationId: 'app-1',
          fromStatus: 'DRAFT',
          toStatus: 'SUBMITTED',
          actorId: 'u-client',
          actorRole: 'Client',
          occurredAt: '2026-10-02T09:00:00Z',
        },
        {
          id: 'e2',
          applicationId: 'app-1',
          fromStatus: 'SUBMITTED',
          toStatus: 'UNDER_REVIEW',
          actorId: 'u-analyst',
          actorRole: 'Analyst',
          occurredAt: '2026-10-03T09:00:00Z',
        },
      ],
    }),
    client: { id: 'u-client', firstName: 'Salma', lastName: 'Bennani', email: 'client@test.ma' },
    clientProfile: COMPLETE_PROFILE,
    scoringBreakdown: { debtRatio: 40, employment: 22, seniority: 15, amountToIncome: 5 },
    ...overrides,
  };
}
