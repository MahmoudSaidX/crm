import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./shell/portal-shell').then((m) => m.PortalShell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./home/home').then((m) => m.Home),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
