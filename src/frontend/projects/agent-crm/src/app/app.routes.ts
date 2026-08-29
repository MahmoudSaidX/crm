import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';

/**
 * Application routes. Feature routes live under per-capability folders
 * (`app/<capability>/...`) — there is deliberately no global `features/` folder.
 * CRM-117 owns the real application shell and its navigation.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./auth/login').then((m) => m.Login),
  },
  {
    path: '',
    pathMatch: 'full',
    canActivate: [authGuard],
    loadComponent: () => import('./home/home').then((m) => m.Home),
  },
  { path: '**', redirectTo: '' },
];
