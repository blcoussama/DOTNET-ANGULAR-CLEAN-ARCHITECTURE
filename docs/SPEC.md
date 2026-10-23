# CreditFlow — Spécification (source commune backend .NET / frontend Angular)

Plateforme de gestion des **demandes de crédit** d'une banque : le client simule et dépose une demande avec ses pièces justificatives ; un analyste étudie le dossier, aidé d'un **score de risque** calculé automatiquement, puis approuve, refuse ou demande des compléments ; un administrateur gère les utilisateurs et les produits. La **décision est toujours humaine** : le score est une aide, jamais une décision automatique.

## 1. Rôles
| Rôle | Peut |
|---|---|
| `Client` | s'inscrire, gérer son profil financier, simuler, créer / modifier (brouillon ou complément demandé) / soumettre / annuler ses demandes, déposer des pièces, suivre l'historique |
| `Analyst` | voir la file des demandes soumises, s'assigner un dossier, voir le scoring détaillé et les pièces, approuver / refuser / demander des compléments (commentaire obligatoire), tableau de bord |
| `Admin` | tout ce que fait l'analyste en lecture + gérer les utilisateurs (créer un analyste, activer / désactiver, changer le rôle), gérer les produits de crédit (taux, bornes) |

Un client ne voit **que ses propres** demandes et pièces (contrôle par ressource, 404 sinon). Un compte désactivé ne peut plus se connecter et ses jetons sont révoqués.

## 2. Domaine
- **ClientProfile** (1–1 avec l'utilisateur client) : `dateOfBirth`, `phone`, `city`, `employmentType` ∈ `CDI | CDD | FONCTIONNAIRE | INDEPENDANT | RETRAITE | SANS_EMPLOI`, `employer?`, `monthlyIncome` (MAD, > 0), `monthlyCharges` (MAD, ≥ 0, mensualités déjà en cours), `yearsAtCurrentJob` (≥ 0). Profil **complet obligatoire** avant de soumettre une demande.
- **CreditProduct** (paramétrable par l'admin, données initiales) : `type` ∈ `CONSOMMATION | AUTO | IMMOBILIER`, `annualRate` (%), `minAmount`, `maxAmount`, `minDurationMonths`, `maxDurationMonths`, `isActive`.
  Valeurs initiales : CONSOMMATION 7,5 %, 5 000–300 000 MAD, 6–84 mois · AUTO 6,5 %, 20 000–600 000, 12–84 · IMMOBILIER 4,9 %, 100 000–5 000 000, 60–300.
- **CreditApplication** : `id` (GUID), `reference` lisible unique (`CF-AAAA-NNNNNN`), `clientId`, `productType`, `amount`, `durationMonths`, `purpose` (≤ 500), `status`, `annualRate` (figé à la soumission), `monthlyPayment`, `totalCost`, `debtRatio`, `riskScore` (0–100), `riskGrade` (A–E), `recommendation`, `assignedAnalystId?`, `decisionComment?`, `createdAt`, `submittedAt?`, `decidedAt?`, **jeton de concurrence** (modification simultanée ⇒ 409).
- **ApplicationDocument** : `id`, `applicationId`, `type` ∈ `ID_CARD | PAYSLIP | BANK_STATEMENT | OTHER`, `fileName`, `contentType`, `sizeBytes`, `uploadedAt`. Fichiers PDF / JPEG / PNG, **5 Mo max**, type vérifié par **signature binaire** (magic bytes), stockés hors de la racine web (dossier configurable, derrière une abstraction `IFileStorage`), nom de stockage aléatoire (jamais le nom fourni).
- **ApplicationEvent** (historique / audit, jamais modifié) : `id`, `applicationId`, `fromStatus?`, `toStatus`, `actorId`, `actorRole`, `comment?`, `occurredAt`.

### Cycle de vie (machine à états, dans le domaine, testée)
```
DRAFT --submit--> SUBMITTED --assign--> UNDER_REVIEW --approve--> APPROVED
  |                   |                      |--reject---> REJECTED
  |                   |                      '--request-info--> INFO_REQUESTED --submit--> SUBMITTED
  '--cancel-->  CANCELLED <--cancel-- (SUBMITTED, INFO_REQUESTED : par le client)
```
Toute transition non listée ⇒ 409 (message clair). Chaque transition crée un `ApplicationEvent`. `approve`/`reject`/`request-info` : commentaire obligatoire (≤ 1000). Seul l'analyste **assigné** (ou un admin) décide.

### Scoring (service de domaine pur, sans base, entièrement testé)
- Mensualité (annuité) : `r = annualRate / 12 / 100`, `M = P·r / (1 − (1 + r)^−n)` (si `r = 0` : `P / n`) ; `totalCost = M·n − P` ; arrondis à 2 décimales (MAD).
- Taux d'endettement : `debtRatio = (monthlyCharges + M) / monthlyIncome`.
- Score /100 = somme de :
  - endettement : ≤ 33 % → 40 · ≤ 40 % → 25 · ≤ 50 % → 10 · sinon 0
  - stabilité de l'emploi : FONCTIONNAIRE 25 · CDI 22 · RETRAITE 18 · CDD 12 · INDEPENDANT 10 · SANS_EMPLOI 0
  - ancienneté : ≥ 5 ans 15 · ≥ 2 ans 10 · ≥ 1 an 5 · sinon 0
  - montant / revenu annuel : ≤ 1 → 20 · ≤ 3 → 12 · ≤ 5 → 6 · sinon 0 (pour IMMOBILIER, seuils ×4)
- Grade : A ≥ 80 · B ≥ 65 · C ≥ 50 · D ≥ 35 · E < 35.
- Recommandation : `DEFAVORABLE` si `debtRatio` > seuil (40 % par défaut, configurable) ou grade D/E ; `FAVORABLE` si grade A/B et seuil respecté ; sinon `A_ETUDIER`.
- Le détail (points par critère) est renvoyé à l'analyste (`scoringBreakdown`) pour qu'il comprenne le score.

## 3. API REST (préfixe `/api/v1`, JSON, camelCase, enums en chaînes)
Erreurs : **ProblemDetails (RFC 7807)**, validation ⇒ 400 avec `errors` par champ. Pagination : `?page=1&pageSize=20` (max 100) ⇒ `{ items, page, pageSize, totalCount, totalPages }`. Dates ISO 8601 UTC. Montants : nombres décimaux (MAD).

**Auth** (`/auth`) — `POST /register` `{ email, password, firstName, lastName }` ⇒ 201 (rôle Client) · `POST /login` `{ email, password }` ⇒ `{ accessToken, expiresIn, user: { id, email, firstName, lastName, role } }` + cookie httpOnly `refreshToken` (Path=/api/v1/auth, SameSite=Strict, Secure hors dev) · `POST /refresh` (cookie) ⇒ même réponse, **rotation** + détection de rejeu (rejeu ⇒ révocation de toute la famille) · `POST /logout` · `GET /me`.
JWT d'accès 15 min (clé ≥ 256 bits fournie par configuration, démarrage refusé sinon) ; refresh 7 jours stocké **haché** (SHA-256). Verrouillage du compte après 5 échecs (Identity). Limitation de débit sur `/auth/*` (limiteur intégré ASP.NET Core) ⇒ 429.

**Profil client** — `GET /clients/me/profile` · `PUT /clients/me/profile` (renvoie aussi `isComplete`).

**Simulation** (public) — `POST /simulations` `{ productType, amount, durationMonths, monthlyIncome?, monthlyCharges? }` ⇒ `{ annualRate, monthlyPayment, totalCost, debtRatio?, withinDebtThreshold? }` (bornes du produit validées).

**Produits** — `GET /products` (public, actifs) · Admin : `GET /admin/products`, `PUT /admin/products/{type}`.

**Demandes (client)** — `POST /applications` (brouillon) · `GET /applications/mine` (paginé, filtre `status`) · `GET /applications/{id}` (détail + documents + événements) · `PUT /applications/{id}` (DRAFT ou INFO_REQUESTED) · `POST /applications/{id}/submit` (profil complet + au moins une pièce `ID_CARD` et une `PAYSLIP` ; calcule et fige taux, mensualité, endettement, score) · `POST /applications/{id}/cancel` · `POST /applications/{id}/documents` (multipart : `type`, `file`) · `GET /applications/{id}/documents/{docId}` (téléchargement, propriétaire ou analyste/admin) · `DELETE /applications/{id}/documents/{docId}` (DRAFT / INFO_REQUESTED).

**Instruction (analyste / admin)** — `GET /review/applications` (paginé ; filtres `status`, `riskGrade`, `productType`, `assignedToMe`, `search` (référence ou nom), tri `submittedAt|amount|riskScore` asc/desc) · `GET /review/applications/{id}` (détail + profil client + `scoringBreakdown` + documents + événements) · `POST /review/applications/{id}/assign` · `POST /review/applications/{id}/approve` `{ comment }` · `POST /review/applications/{id}/reject` `{ comment }` · `POST /review/applications/{id}/request-info` `{ comment }`.

**Tableau de bord (analyste / admin)** — `GET /dashboard/stats` ⇒ `{ countsByStatus, approvalRate, averageAmount, totalApprovedAmount, volumeByProduct[], averageProcessingDays, monthlyTrend[6] { month, submitted, approved, rejected }, gradeDistribution }`.

**Utilisateurs (admin)** — `GET /admin/users` (paginé, filtres `role`, `search`) · `POST /admin/users` `{ email, firstName, lastName, role: Analyst|Admin, temporaryPassword }` · `PATCH /admin/users/{id}` `{ role?, isActive? }` (un admin ne peut pas se désactiver ni se retirer son propre rôle).

**Divers** — `GET /health` (sans auth) · OpenAPI `/openapi/v1.json` + interface Scalar `/scalar` (environnement Development uniquement).

**Comptes de démonstration** (seed uniquement en Development, mots de passe lus dans la configuration, jamais en dur dans le dépôt) : un admin, un analyste, un client avec quelques demandes à différents statuts.

## 4. Frontend Angular (dossier `frontend/`)
Pages (UI en français) : accueil + **simulateur de crédit** (public, calcul en direct) · connexion / inscription · **client** : profil financier, mes demandes (liste + statuts), nouvelle demande (**stepper** : produit → montant & durée avec simulation en direct et alerte d'endettement → pièces justificatives → récapitulatif → soumission), détail d'une demande (timeline des événements, pièces, statut, réponse à une demande de compléments) · **analyste** : file d'attente (table paginée, filtres, tri), dossier (profil client, **décomposition du score**, pièces téléchargeables, timeline, actions assigner / approuver / refuser / demander des compléments avec commentaire), tableau de bord (indicateurs + graphiques) · **admin** : utilisateurs, produits.
Règles : jeton d'accès **en mémoire uniquement** (jamais localStorage/sessionStorage), refresh silencieux via cookie au démarrage et sur 401 (un seul refresh partagé), gardes de routes par rôle, aucun `innerHTML` sur des données serveur, formulaires réactifs typés avec validation alignée sur l'API, états chargement / vide / erreur partout, accessibilité (labels, focus, contrastes).

## 5. Qualité attendue
Clean Architecture (`Domain` sans dépendance, `Application` = cas d'usage + interfaces, `Infrastructure` = EF Core / Identity / stockage, `Api` = contrôleurs fins). Tests : domaine (machine à états, scoring), application (cas d'usage), **intégration** (API réelle via `WebApplicationFactory` + vraie base). Aucun secret dans le dépôt. Lint / format propres. README honnête.
