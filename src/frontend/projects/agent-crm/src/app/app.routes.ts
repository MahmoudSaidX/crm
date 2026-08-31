import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';
import { requirePermission } from './auth/permission.guard';

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
        canActivate: [requirePermission('roles.view')],
        loadComponent: () => import('./roles/role-list').then((m) => m.RoleList),
      },
      {
        path: 'roles/new',
        canActivate: [requirePermission('roles.manage')],
        loadComponent: () => import('./roles/role-form').then((m) => m.RoleForm),
      },
      {
        path: 'roles/:id/edit',
        canActivate: [requirePermission('roles.manage')],
        loadComponent: () => import('./roles/role-form').then((m) => m.RoleForm),
      },
      {
        path: 'roles/:id/permissions',
        canActivate: [requirePermission('roles.manage')],
        loadComponent: () => import('./roles/role-permissions').then((m) => m.RolePermissions),
      },
      {
        path: 'forbidden',
        loadComponent: () => import('./auth/forbidden').then((m) => m.Forbidden),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
