import { Routes } from '@angular/router';

export const ANALYST_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'file' },
  {
    path: 'file',
    title: "File d'attente",
    loadComponent: () => import('./queue/review-queue-page').then((m) => m.ReviewQueuePage),
  },
  {
    path: 'dossiers/:id',
    title: 'Dossier de crédit',
    loadComponent: () => import('./review/review-detail-page').then((m) => m.ReviewDetailPage),
  },
  {
    path: 'tableau-de-bord',
    title: 'Tableau de bord',
    loadComponent: () => import('./dashboard/dashboard-page').then((m) => m.DashboardPage),
  },
];
