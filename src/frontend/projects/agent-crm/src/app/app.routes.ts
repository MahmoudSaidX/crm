import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./auth/login').then((m) => m.Login),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell/agent-shell').then((m) => m.AgentShell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./home/home').then((m) => m.Home),
      },
      {
        path: 'roles',
        loadComponent: () => import('./roles/role-list').then((m) => m.RoleList),
      },
      {
        path: 'roles/new',
        loadComponent: () => import('./roles/role-form').then((m) => m.RoleForm),
      },
      {
        path: 'roles/:id/edit',
        loadComponent: () => import('./roles/role-form').then((m) => m.RoleForm),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
