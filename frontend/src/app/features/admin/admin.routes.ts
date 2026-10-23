import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'utilisateurs' },
  {
    path: 'utilisateurs',
    title: 'Utilisateurs',
    loadComponent: () => import('./users/users-page').then((m) => m.UsersPage),
  },
  {
    path: 'produits',
    title: 'Produits de crédit',
    loadComponent: () => import('./products/products-page').then((m) => m.ProductsPage),
  },
];
