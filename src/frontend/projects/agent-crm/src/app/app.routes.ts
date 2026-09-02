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
        path: 'departments',
        canActivate: [requirePermission('departments.view')],
        loadComponent: () => import('./departments/department-list').then((m) => m.DepartmentList),
      },
      {
        path: 'departments/new',
        canActivate: [requirePermission('departments.manage')],
        loadComponent: () => import('./departments/department-form').then((m) => m.DepartmentForm),
      },
      {
        path: 'departments/:id/edit',
        canActivate: [requirePermission('departments.manage')],
        loadComponent: () => import('./departments/department-form').then((m) => m.DepartmentForm),
      },
      {
        path: 'branches',
        canActivate: [requirePermission('branches.view')],
        loadComponent: () => import('./branches/branch-list').then((m) => m.BranchList),
      },
      {
        path: 'branches/new',
        canActivate: [requirePermission('branches.manage')],
        loadComponent: () => import('./branches/branch-form').then((m) => m.BranchForm),
      },
      {
        path: 'branches/:id/edit',
        canActivate: [requirePermission('branches.manage')],
        loadComponent: () => import('./branches/branch-form').then((m) => m.BranchForm),
      },
      {
        path: 'customers/new',
        canActivate: [requirePermission('customers.manage')],
        loadComponent: () => import('./customers/customer-form').then((m) => m.CustomerForm),
      },
      {
        path: 'branding',
        canActivate: [requirePermission('branding.view')],
        loadComponent: () => import('./branding/branding-settings').then((m) => m.BrandingSettings),
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
        path: 'system-configuration',
        canActivate: [requirePermission('configuration.view')],
        loadComponent: () =>
          import('./system-configuration/system-configuration-list').then(
            (m) => m.SystemConfigurationList,
          ),
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
