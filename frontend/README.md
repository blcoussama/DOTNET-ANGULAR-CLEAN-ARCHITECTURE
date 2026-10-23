# CreditFlow — frontend Angular

Interface de la plateforme CreditFlow (contrat : `../docs/SPEC.md`). Angular 22 (composants standalone,
signals, zoneless), Angular Material 3, `ng2-charts` / Chart.js, UI en français (MAD, dates fr-FR).

## Commandes

| Commande | Rôle |
|---|---|
| `npm start` | serveur de dev sur http://localhost:4200, `/api` relayé vers `http://localhost:5080` (`proxy.conf.json`) |
| `npm test` | tests unitaires Vitest (jsdom), non interactif ; `npm run test:watch` pour le mode veille |
| `npm run lint` | ESLint (`angular-eslint`, règles d'accessibilité des templates, règles de sécurité) |
| `npm run build` | build de production dans `dist/creditflow/browser` (SPA : servir `index.html` pour toute route inconnue) |

## Organisation

```
src/app/
  core/       api/ (services typés /api/v1 + normalisation), auth/ (store en mémoire, intercepteur, gardes),
              http/ (ProblemDetails), i18n/ (libellés, titres, pagination), util/ (redirection sûre, fichiers, prêt)
  shared/     composants (badges, timeline, états chargement/vide/erreur, pièces, simulation), pipes, services
  layout/     coquille (barre, navigation par rôle, déconnexion)
  features/   public/ (accueil + simulateur, 404), auth/, client/, analyst/, admin/  — toutes chargées à la demande
```

## Sécurité

- Jeton d'accès **en mémoire uniquement** (signal) ; `localStorage` / `sessionStorage` interdits par ESLint.
- Session restaurée au démarrage par `POST /api/v1/auth/refresh` (cookie httpOnly, `withCredentials`).
- Sur 401 : un seul refresh partagé (et sérialisé entre onglets via Web Locks), puis rejeu unique.
- Le jeton n'est envoyé qu'aux URL relatives `/api/v1` ; redirection après connexion limitée aux chemins internes.
- Aucun `innerHTML` ni `bypassSecurityTrust*` (interdits par ESLint) ; pièces téléchargées en Blob via `HttpClient`.
- Build sans script inline (`inlineCritical: false`) : `script-src 'self'` possible ; les styles des composants
  sont injectés à l'exécution (`style-src 'self' 'unsafe-inline'`). Polices et icônes auto-hébergées (aucun CDN).
