import {
  ApplicationStatus,
  DocumentType,
  EmploymentType,
  ProductType,
  Recommendation,
  RiskGrade,
  Role,
} from '../models/api.models';

export const ROLE_LABELS: Record<Role, string> = {
  Client: 'Client',
  Analyst: 'Analyste',
  Admin: 'Administrateur',
};

export const STATUS_LABELS: Record<ApplicationStatus, string> = {
  DRAFT: 'Brouillon',
  SUBMITTED: 'Soumise',
  UNDER_REVIEW: "En cours d'étude",
  INFO_REQUESTED: 'Compléments demandés',
  APPROVED: 'Approuvée',
  REJECTED: 'Refusée',
  CANCELLED: 'Annulée',
};

export const PRODUCT_LABELS: Record<ProductType, string> = {
  CONSOMMATION: 'Crédit consommation',
  AUTO: 'Crédit auto',
  IMMOBILIER: 'Crédit immobilier',
};

export const PRODUCT_DESCRIPTIONS: Record<ProductType, string> = {
  CONSOMMATION: 'Financez vos projets personnels : travaux, équipement, études.',
  AUTO: 'Achetez un véhicule neuf ou d’occasion.',
  IMMOBILIER: 'Acquérez ou construisez votre logement.',
};

export const PRODUCT_ICONS: Record<ProductType, string> = {
  CONSOMMATION: 'shopping_bag',
  AUTO: 'directions_car',
  IMMOBILIER: 'home',
};

export const EMPLOYMENT_LABELS: Record<EmploymentType, string> = {
  CDI: 'CDI',
  CDD: 'CDD',
  FONCTIONNAIRE: 'Fonctionnaire',
  INDEPENDANT: 'Indépendant',
  RETRAITE: 'Retraité',
  SANS_EMPLOI: 'Sans emploi',
};

export const DOCUMENT_LABELS: Record<DocumentType, string> = {
  ID_CARD: "Carte d'identité",
  PAYSLIP: 'Bulletin de salaire',
  BANK_STATEMENT: 'Relevé bancaire',
  OTHER: 'Autre justificatif',
};

export const RECOMMENDATION_LABELS: Record<Recommendation, string> = {
  FAVORABLE: 'Favorable',
  A_ETUDIER: 'À étudier',
  DEFAVORABLE: 'Défavorable',
};

export const GRADE_LABELS: Record<RiskGrade, string> = {
  A: 'A — risque très faible',
  B: 'B — risque faible',
  C: 'C — risque modéré',
  D: 'D — risque élevé',
  E: 'E — risque très élevé',
};

/** Seuil d'endettement par défaut (SPEC §2 : 40 %, configurable côté serveur). */
export const DEFAULT_DEBT_THRESHOLD = 0.4;
