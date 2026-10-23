import { Routes } from '@angular/router';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { guestGuard, roleGuard } from '../core/auth/auth.guards';
import { FrenchPaginatorIntl } from '../core/i18n/paginator-intl';

/**
 * Toutes les pages, chargées à la demande. Les réglages Material communs (champs « outline »,
 * pagination en français) sont fournis ici pour ne pas alourdir le bundle initial.
 */
export const FEATURE_ROUTES: Routes = [
  {
    path: '',
    providers: [
      { provide: MatPaginatorIntl, useClass: FrenchPaginatorIntl },
      {
        provide: MAT_FORM_FIELD_DEFAULT_OPTIONS,
        useValue: { appearance: 'outline', subscriptSizing: 'dynamic' },
      },
    ],
    children: [
      {
        path: '',
        pathMatch: 'full',
        title: 'Simulateur de crédit',
        loadComponent: () => import('./public/home/home-page').then((m) => m.HomePage),
      },
      {
        path: 'connexion',
        title: 'Connexion',
        canActivate: [guestGuard],
        loadComponent: () => import('./auth/login-page').then((m) => m.LoginPage),
      },
      {
        path: 'inscription',
        title: 'Créer un compte',
        canActivate: [guestGuard],
        loadComponent: () => import('./auth/register-page').then((m) => m.RegisterPage),
      },
      {
        path: 'client',
        canMatch: [roleGuard('Client')],
        loadChildren: () => import('./client/client.routes').then((m) => m.CLIENT_ROUTES),
      },
      {
        path: 'analyste',
        canMatch: [roleGuard('Analyst', 'Admin')],
        loadChildren: () => import('./analyst/analyst.routes').then((m) => m.ANALYST_ROUTES),
      },
      {
        path: 'admin',
        canMatch: [roleGuard('Admin')],
        loadChildren: () => import('./admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      {
        path: 'acces-refuse',
        title: 'Accès refusé',
        loadComponent: () => import('./public/forbidden-page').then((m) => m.ForbiddenPage),
      },
      {
        path: '**',
        title: 'Page introuvable',
        loadComponent: () => import('./public/not-found-page').then((m) => m.NotFoundPage),
      },
    ],
  },
];
