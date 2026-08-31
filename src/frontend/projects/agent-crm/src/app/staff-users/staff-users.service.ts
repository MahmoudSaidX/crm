import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PagedResult, Role } from '../roles/roles.service';

export interface StaffUser {
  readonly id: string;
  readonly email: string;
  readonly displayName: string | null;
  readonly department: string | null;
  readonly branch: string | null;
  readonly isActive: boolean;
  readonly createdAtUtc: string;
}

export interface CreateStaffUserRequest {
  readonly email: string;
  readonly password: string;
  readonly displayName: string | null;
  readonly department: string | null;
  readonly branch: string | null;
}

export interface UpdateStaffUserRequest {
  readonly displayName: string | null;
  readonly department: string | null;
  readonly branch: string | null;
}

export interface RoleSummary {
  readonly id: string;
  readonly name: string;
  readonly code: string;
}

/**
 * Staff CRUD/search/activation is gated by users.view/users.manage; role
 * assignment reuses the existing roles.view/roles.manage policies since it
 * is a role-management action (CRM-113), not a new permission axis.
 * Department/Branch are plain free-text fields — no CRM-118/119 scope.
 */
@Injectable({ providedIn: 'root' })
export class StaffUsersService {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number, search?: string): Promise<PagedResult<StaffUser>> {
    return firstValueFrom(
      this.http.get<PagedResult<StaffUser>>('/api/v1/staff-users', {
        params: search ? { page, pageSize, search } : { page, pageSize },
      }),
    );
  }

  get(id: string): Promise<StaffUser> {
    return firstValueFrom(this.http.get<StaffUser>(`/api/v1/staff-users/${id}`));
  }

  create(request: CreateStaffUserRequest): Promise<StaffUser> {
    return firstValueFrom(this.http.post<StaffUser>('/api/v1/staff-users', request));
  }

  update(id: string, request: UpdateStaffUserRequest): Promise<StaffUser> {
    return firstValueFrom(this.http.put<StaffUser>(`/api/v1/staff-users/${id}`, request));
  }

  activate(id: string): Promise<StaffUser> {
    return firstValueFrom(this.http.post<StaffUser>(`/api/v1/staff-users/${id}/activate`, {}));
  }

  deactivate(id: string): Promise<StaffUser> {
    return firstValueFrom(this.http.post<StaffUser>(`/api/v1/staff-users/${id}/deactivate`, {}));
  }

  roles(staffSubjectId: string): Promise<readonly RoleSummary[]> {
    return firstValueFrom(
      this.http.get<readonly RoleSummary[]>(`/api/v1/staff-users/${staffSubjectId}/roles`),
    );
  }

  replaceRoles(staffSubjectId: string, roleIds: readonly string[]): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/v1/staff-users/${staffSubjectId}/roles`, { roleIds }),
    );
  }
}

export type { Role };
