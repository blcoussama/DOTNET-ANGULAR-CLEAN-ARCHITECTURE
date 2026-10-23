import { Routes } from '@angular/router';
import { Shell } from './layout/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    loadChildren: () => import('./features/feature.routes').then((m) => m.FEATURE_ROUTES),
  },
];
