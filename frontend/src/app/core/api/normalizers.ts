import {
  APPLICATION_STATUSES,
  ApplicationStatus,
  DashboardStats,
  DashboardStatsRaw,
  PRODUCT_TYPES,
  ProductType,
  ProductVolume,
  RISK_GRADES,
  ReviewApplicationDetail,
  ReviewApplicationDetailRaw,
  RiskGrade,
  ScoreCriterion,
} from '../models/api.models';
import { toDateOnly } from './profile.api';

/**
 * Normalisation des formes que la SPEC ne fixe pas précisément (détail du score, répartitions du
 * tableau de bord). Centraliser ces adaptations ici limite l'impact d'un écart de contrat.
 */

interface CriterionDefinition {
  key: string;
  label: string;
  maxPoints: number;
  aliases: string[];
}

export const SCORE_CRITERIA: CriterionDefinition[] = [
  {
    key: 'debtRatio',
    label: "Taux d'endettement",
    maxPoints: 40,
    aliases: ['debtratio', 'debt', 'endettement'],
  },
  {
    key: 'employment',
    label: "Stabilité de l'emploi",
    maxPoints: 25,
    aliases: [
      'employment',
      'employmenttype',
      'employmentstability',
      'stability',
      'jobstability',
      'emploi',
    ],
  },
  {
    key: 'seniority',
    label: 'Ancienneté',
    maxPoints: 15,
    aliases: ['seniority', 'yearsatcurrentjob', 'jobseniority', 'anciennete', 'tenure'],
  },
  {
    key: 'amountToIncome',
    label: 'Montant / revenu annuel',
    maxPoints: 20,
    aliases: [
      'amounttoincome',
      'amounttoannualincome',
      'amountincomeratio',
      'loantoincome',
      'amountratio',
      'montantrevenu',
    ],
  },
];

const TOTAL_KEYS = [
  'total',
  'totalscore',
  'score',
  'riskscore',
  'grade',
  'riskgrade',
  'recommendation',
];

function canonical(key: string): string {
  return key
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]/g, '')
    .replace(/(points|score|pts)$/, '');
}

function findDefinition(key: string): CriterionDefinition | undefined {
  const c = canonical(key);
  return SCORE_CRITERIA.find((d) => canonical(d.key) === c || d.aliases.includes(c));
}

function toNumber(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string' && value.trim() !== '' && Number.isFinite(Number(value)))
    return Number(value);
  return null;
}

function pick(obj: Record<string, unknown>, ...keys: string[]): unknown {
  for (const key of keys) {
    if (obj[key] !== undefined && obj[key] !== null) return obj[key];
  }
  return undefined;
}

function buildCriterion(rawKey: string, value: unknown): ScoreCriterion | null {
  let points: number | null;
  let maxPoints: number | null = null;
  let label: string | null = null;
  let detail: string | null = null;
  if (typeof value === 'object' && value !== null) {
    const obj = value as Record<string, unknown>;
    points = toNumber(pick(obj, 'points', 'score', 'value'));
    maxPoints = toNumber(pick(obj, 'maxPoints', 'max', 'maximum', 'outOf'));
    const rawLabel = pick(obj, 'label');
    label = typeof rawLabel === 'string' ? rawLabel : null;
    const rawDetail = pick(obj, 'detail', 'description', 'explanation', 'reason');
    detail = typeof rawDetail === 'string' ? rawDetail : null;
  } else {
    points = toNumber(value);
  }
  if (points === null) return null;
  const definition = findDefinition(rawKey);
  return {
    key: definition?.key ?? rawKey,
    label: definition?.label ?? label ?? rawKey,
    points,
    maxPoints: maxPoints ?? definition?.maxPoints ?? Math.max(points, 1),
    detail,
  };
}

/** Accepte un tableau `[{ criterion, points, maxPoints }]` ou un objet `{ debtRatio: 40, … }`. */
export function normalizeScoringBreakdown(raw: unknown): ScoreCriterion[] {
  const result: ScoreCriterion[] = [];
  if (Array.isArray(raw)) {
    for (const item of raw) {
      if (typeof item !== 'object' || item === null) continue;
      const obj = item as Record<string, unknown>;
      const key = pick(obj, 'criterion', 'key', 'name', 'code', 'label');
      if (typeof key !== 'string') continue;
      const criterion = buildCriterion(key, obj);
      if (criterion) result.push(criterion);
    }
  } else if (typeof raw === 'object' && raw !== null) {
    const source = raw as Record<string, unknown>;
    const nested = source['criteria'] ?? source['items'];
    if (Array.isArray(nested)) return normalizeScoringBreakdown(nested);
    for (const [key, value] of Object.entries(source)) {
      if (TOTAL_KEYS.includes(canonical(key)) || TOTAL_KEYS.includes(key.toLowerCase())) continue;
      const criterion = buildCriterion(key, value);
      if (criterion) result.push(criterion);
    }
  }
  const order = (c: ScoreCriterion) => {
    const index = SCORE_CRITERIA.findIndex((d) => d.key === c.key);
    return index === -1 ? SCORE_CRITERIA.length : index;
  };
  return result.sort((a, b) => order(a) - order(b));
}

export function normalizeReviewDetail(raw: ReviewApplicationDetailRaw): ReviewApplicationDetail {
  const fromClient = [raw.client?.firstName, raw.client?.lastName].filter(Boolean).join(' ');
  const profile = raw.clientProfile ?? null;
  return {
    ...raw,
    documents: raw.documents ?? [],
    events: raw.events ?? [],
    clientName:
      raw.clientName?.trim() || fromClient || raw.client?.email || raw.clientEmail || 'Client',
    clientEmail: raw.clientEmail ?? raw.client?.email ?? null,
    clientProfile: profile ? { ...profile, dateOfBirth: toDateOnly(profile.dateOfBirth) } : null,
    scoringBreakdown: normalizeScoringBreakdown(raw.scoringBreakdown),
    assignedAnalystName: raw.assignedAnalystName ?? null,
  };
}

/** `{ A: 3 }` ou `[{ grade: 'A', count: 3 }]` ⇒ `{ A: 3, B: 0, … }`. */
export function normalizeCounts<K extends string>(
  raw: unknown,
  keys: readonly K[],
  keyFields: string[],
): Record<K, number> {
  const counts = Object.fromEntries(keys.map((k) => [k, 0])) as Record<K, number>;
  const add = (key: unknown, value: unknown) => {
    if (typeof key !== 'string') return;
    const match = keys.find((k) => k.toLowerCase() === key.toLowerCase());
    const n = toNumber(value);
    if (match && n !== null) counts[match] += n;
  };
  if (Array.isArray(raw)) {
    for (const item of raw) {
      if (typeof item !== 'object' || item === null) continue;
      const obj = item as Record<string, unknown>;
      add(pick(obj, ...keyFields), pick(obj, 'count', 'value', 'total'));
    }
  } else if (typeof raw === 'object' && raw !== null) {
    for (const [key, value] of Object.entries(raw as Record<string, unknown>)) add(key, value);
  }
  return counts;
}

export function normalizeVolumeByProduct(raw: unknown): ProductVolume[] {
  const volumes = new Map<ProductType, ProductVolume>(
    PRODUCT_TYPES.map((t) => [t, { productType: t, count: 0, totalAmount: 0 }]),
  );
  const add = (type: unknown, value: unknown) => {
    if (typeof type !== 'string') return;
    const match = PRODUCT_TYPES.find((t) => t === type.toUpperCase());
    if (!match) return;
    const entry = volumes.get(match)!;
    if (typeof value === 'object' && value !== null) {
      const obj = value as Record<string, unknown>;
      entry.count += toNumber(pick(obj, 'count', 'applications', 'total')) ?? 0;
      entry.totalAmount += toNumber(pick(obj, 'totalAmount', 'amount', 'volume', 'sum')) ?? 0;
    } else {
      entry.count += toNumber(value) ?? 0;
    }
  };
  if (Array.isArray(raw)) {
    for (const item of raw) {
      if (typeof item !== 'object' || item === null) continue;
      const obj = item as Record<string, unknown>;
      add(pick(obj, 'productType', 'type', 'product'), obj);
    }
  } else if (typeof raw === 'object' && raw !== null) {
    for (const [key, value] of Object.entries(raw as Record<string, unknown>)) add(key, value);
  }
  return [...volumes.values()];
}

/** Ramène un taux exprimé en pourcentage (ex. 62,5) à un ratio (0,625). */
export function toRatio(value: number | null | undefined): number {
  const n = toNumber(value) ?? 0;
  return n > 1 ? n / 100 : n;
}

export function normalizeDashboardStats(raw: DashboardStatsRaw): DashboardStats {
  const countsByStatus = normalizeCounts<ApplicationStatus>(
    raw.countsByStatus,
    APPLICATION_STATUSES,
    ['status', 'key'],
  );
  const gradeDistribution = normalizeCounts<RiskGrade>(raw.gradeDistribution, RISK_GRADES, [
    'grade',
    'riskGrade',
    'key',
  ]);
  return {
    countsByStatus,
    totalApplications: Object.values<number>(countsByStatus).reduce((sum, n) => sum + n, 0),
    approvalRate: toRatio(raw.approvalRate),
    averageAmount: toNumber(raw.averageAmount) ?? 0,
    totalApprovedAmount: toNumber(raw.totalApprovedAmount) ?? 0,
    volumeByProduct: normalizeVolumeByProduct(raw.volumeByProduct),
    averageProcessingDays: toNumber(raw.averageProcessingDays) ?? 0,
    monthlyTrend: Array.isArray(raw.monthlyTrend)
      ? raw.monthlyTrend.map((p) => ({
          month: String(p.month),
          submitted: toNumber(p.submitted) ?? 0,
          approved: toNumber(p.approved) ?? 0,
          rejected: toNumber(p.rejected) ?? 0,
        }))
      : [],
    gradeDistribution,
  };
}
