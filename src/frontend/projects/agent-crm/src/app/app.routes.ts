import { Routes } from '@angular/router';

/**
 * Application routes. Feature routes live under per-capability folders
 * (`app/<capability>/...`) — there is deliberately no global `features/` folder.
 * CRM-117 owns the real application shell and its navigation.
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home').then((m) => m.Home),
  },
  { path: '**', redirectTo: '' },
];
