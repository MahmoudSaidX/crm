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
        path: 'staff-users',
        canActivate: [requirePermission('users.view')],
        loadComponent: () => import('./staff-users/staff-user-list').then((m) => m.StaffUserList),
      },
      {
        path: 'staff-users/new',
        canActivate: [requirePermission('users.manage')],
        loadComponent: () => import('./staff-users/staff-user-form').then((m) => m.StaffUserForm),
      },
      {
        path: 'staff-users/:id/edit',
        canActivate: [requirePermission('users.manage')],
        loadComponent: () => import('./staff-users/staff-user-form').then((m) => m.StaffUserForm),
      },
      {
        path: 'staff-users/:id/roles',
        canActivate: [requirePermission('users.manage')],
        loadComponent: () => import('./staff-users/staff-user-roles').then((m) => m.StaffUserRoles),
      },
      {
        path: 'audit',
        canActivate: [requirePermission('audit.view')],
        loadComponent: () => import('./audit/audit-list').then((m) => m.AuditList),
      },
      {
        path: 'audit/:id',
        canActivate: [requirePermission('audit.view')],
        loadComponent: () => import('./audit/audit-detail').then((m) => m.AuditDetail),
      },
      {
        path: 'forbidden',
        loadComponent: () => import('./auth/forbidden').then((m) => m.Forbidden),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
