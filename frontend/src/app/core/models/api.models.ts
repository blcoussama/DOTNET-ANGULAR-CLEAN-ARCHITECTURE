/**
 * Types du contrat REST CreditFlow (docs/SPEC.md §2 et §3).
 * JSON camelCase, enums sérialisés en chaînes, dates ISO 8601 UTC, montants en MAD.
 */

export const ROLES = ['Client', 'Analyst', 'Admin'] as const;
export type Role = (typeof ROLES)[number];

export const APPLICATION_STATUSES = [
  'DRAFT',
  'SUBMITTED',
  'UNDER_REVIEW',
  'INFO_REQUESTED',
  'APPROVED',
  'REJECTED',
  'CANCELLED',
] as const;
export type ApplicationStatus = (typeof APPLICATION_STATUSES)[number];

export const PRODUCT_TYPES = ['CONSOMMATION', 'AUTO', 'IMMOBILIER'] as const;
export type ProductType = (typeof PRODUCT_TYPES)[number];

export const EMPLOYMENT_TYPES = [
  'CDI',
  'CDD',
  'FONCTIONNAIRE',
  'INDEPENDANT',
  'RETRAITE',
  'SANS_EMPLOI',
] as const;
export type EmploymentType = (typeof EMPLOYMENT_TYPES)[number];

export const DOCUMENT_TYPES = ['ID_CARD', 'PAYSLIP', 'BANK_STATEMENT', 'OTHER'] as const;
export type DocumentType = (typeof DOCUMENT_TYPES)[number];

export const RISK_GRADES = ['A', 'B', 'C', 'D', 'E'] as const;
export type RiskGrade = (typeof RISK_GRADES)[number];

export const RECOMMENDATIONS = ['FAVORABLE', 'A_ETUDIER', 'DEFAVORABLE'] as const;
export type Recommendation = (typeof RECOMMENDATIONS)[number];

/** Réponse paginée commune : `{ items, page, pageSize, totalCount, totalPages }`. */
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PageQuery {
  page: number;
  pageSize: number;
}

// ---------------------------------------------------------------- Auth

export interface AuthUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: Role;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface AuthResponse {
  accessToken: string;
  /** Durée de validité du jeton d'accès, en secondes. */
  expiresIn: number;
  user: AuthUser;
}

// ---------------------------------------------------------------- Profil client

export interface ClientProfile {
  /** Date de naissance `yyyy-MM-dd`. */
  dateOfBirth: string | null;
  phone: string | null;
  city: string | null;
  employmentType: EmploymentType | null;
  employer?: string | null;
  monthlyIncome: number | null;
  monthlyCharges: number | null;
  yearsAtCurrentJob: number | null;
  isComplete?: boolean;
}

export interface ClientProfileUpdate {
  dateOfBirth: string;
  phone: string;
  city: string;
  employmentType: EmploymentType;
  employer: string | null;
  monthlyIncome: number;
  monthlyCharges: number;
  yearsAtCurrentJob: number;
}

// ---------------------------------------------------------------- Produits & simulation

export interface CreditProduct {
  type: ProductType;
  annualRate: number;
  minAmount: number;
  maxAmount: number;
  minDurationMonths: number;
  maxDurationMonths: number;
  isActive: boolean;
}

export type CreditProductUpdate = Omit<CreditProduct, 'type'>;

export interface SimulationRequest {
  productType: ProductType;
  amount: number;
  durationMonths: number;
  monthlyIncome?: number;
  monthlyCharges?: number;
}

export interface SimulationResult {
  annualRate: number;
  monthlyPayment: number;
  totalCost: number;
  /** Ratio (0,35 = 35 %), présent si le revenu est fourni. */
  debtRatio?: number | null;
  withinDebtThreshold?: boolean | null;
}

// ---------------------------------------------------------------- Demandes

export interface CreditApplication {
  id: string;
  reference: string;
  clientId?: string;
  productType: ProductType;
  amount: number;
  durationMonths: number;
  purpose: string | null;
  status: ApplicationStatus;
  annualRate?: number | null;
  monthlyPayment?: number | null;
  totalCost?: number | null;
  debtRatio?: number | null;
  riskScore?: number | null;
  riskGrade?: RiskGrade | null;
  recommendation?: Recommendation | null;
  assignedAnalystId?: string | null;
  decisionComment?: string | null;
  createdAt: string;
  submittedAt?: string | null;
  decidedAt?: string | null;
}

export interface ApplicationDocument {
  id: string;
  applicationId: string;
  type: DocumentType;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface ApplicationEvent {
  id: string;
  applicationId: string;
  fromStatus?: ApplicationStatus | null;
  toStatus: ApplicationStatus;
  actorId: string;
  actorRole: Role;
  actorName?: string | null;
  comment?: string | null;
  occurredAt: string;
}

export interface ApplicationDetail extends CreditApplication {
  documents: ApplicationDocument[];
  events: ApplicationEvent[];
}

export interface ApplicationDraft {
  productType: ProductType;
  amount: number;
  durationMonths: number;
  purpose: string;
}

export interface MyApplicationsQuery extends PageQuery {
  status?: ApplicationStatus | null;
}

// ---------------------------------------------------------------- Instruction (analyste)

export interface ReviewApplicationSummary extends CreditApplication {
  clientName?: string | null;
  clientEmail?: string | null;
  assignedAnalystName?: string | null;
}

export type ReviewSortField = 'submittedAt' | 'amount' | 'riskScore';
export type SortDirection = 'asc' | 'desc';

export interface ReviewQuery extends PageQuery {
  status?: ApplicationStatus | null;
  riskGrade?: RiskGrade | null;
  productType?: ProductType | null;
  assignedToMe?: boolean;
  search?: string | null;
  sortBy?: ReviewSortField | null;
  sortDir?: SortDirection | null;
}

export type ScoreCriterionKey = 'debtRatio' | 'employment' | 'seniority' | 'amountToIncome';

/** Critère du score, normalisé côté frontend (voir `normalizeScoringBreakdown`). */
export interface ScoreCriterion {
  key: ScoreCriterionKey | string;
  label: string;
  points: number;
  maxPoints: number;
  detail?: string | null;
}

export interface ReviewClient {
  id?: string;
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
}

/** Forme brute renvoyée par `GET /review/applications/{id}`. */
export interface ReviewApplicationDetailRaw extends ApplicationDetail {
  clientName?: string | null;
  clientEmail?: string | null;
  client?: ReviewClient | null;
  clientProfile?: ClientProfile | null;
  scoringBreakdown?: unknown;
  assignedAnalystName?: string | null;
}

/** Vue normalisée du dossier analyste. */
export interface ReviewApplicationDetail extends ApplicationDetail {
  clientName: string;
  clientEmail: string | null;
  clientProfile: ClientProfile | null;
  scoringBreakdown: ScoreCriterion[];
  assignedAnalystName: string | null;
}

export interface DecisionRequest {
  comment: string;
}

// ---------------------------------------------------------------- Tableau de bord

export interface MonthlyTrendPoint {
  month: string;
  submitted: number;
  approved: number;
  rejected: number;
}

export interface ProductVolume {
  productType: ProductType;
  count: number;
  totalAmount: number;
}

/** Forme brute de `GET /dashboard/stats` (les répartitions peuvent être des objets ou des tableaux). */
export interface DashboardStatsRaw {
  countsByStatus: unknown;
  approvalRate: number;
  averageAmount: number;
  totalApprovedAmount: number;
  volumeByProduct: unknown;
  averageProcessingDays: number;
  monthlyTrend: MonthlyTrendPoint[];
  gradeDistribution: unknown;
}

export interface DashboardStats {
  countsByStatus: Record<ApplicationStatus, number>;
  totalApplications: number;
  /** Ratio 0–1. */
  approvalRate: number;
  averageAmount: number;
  totalApprovedAmount: number;
  volumeByProduct: ProductVolume[];
  averageProcessingDays: number;
  monthlyTrend: MonthlyTrendPoint[];
  gradeDistribution: Record<RiskGrade, number>;
}

// ---------------------------------------------------------------- Administration

export interface AdminUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: Role;
  isActive: boolean;
  createdAt?: string | null;
}

export interface AdminUsersQuery extends PageQuery {
  role?: Role | null;
  search?: string | null;
}

export type StaffRole = Extract<Role, 'Analyst' | 'Admin'>;

export interface CreateStaffUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  role: StaffRole;
  temporaryPassword: string;
}

export interface UpdateUserRequest {
  role?: Role;
  isActive?: boolean;
}
