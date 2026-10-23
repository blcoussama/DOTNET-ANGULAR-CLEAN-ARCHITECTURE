import { Routes } from '@angular/router';

export const CLIENT_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'demandes' },
  {
    path: 'profil',
    title: 'Profil financier',
    loadComponent: () => import('./profile/profile-page').then((m) => m.ProfilePage),
  },
  {
    path: 'demandes',
    title: 'Mes demandes',
    loadComponent: () =>
      import('./applications/my-applications-page').then((m) => m.MyApplicationsPage),
  },
  {
    path: 'demandes/nouvelle',
    title: 'Nouvelle demande',
    loadComponent: () =>
      import('./new-application/new-application-page').then((m) => m.NewApplicationPage),
  },
  {
    path: 'demandes/:id/modifier',
    title: 'Modifier la demande',
    loadComponent: () =>
      import('./new-application/new-application-page').then((m) => m.NewApplicationPage),
  },
  {
    path: 'demandes/:id',
    title: 'Détail de la demande',
    loadComponent: () =>
      import('./application-detail/application-detail-page').then((m) => m.ApplicationDetailPage),
  },
];
