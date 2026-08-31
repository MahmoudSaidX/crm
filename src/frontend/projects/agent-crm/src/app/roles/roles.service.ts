import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface Role {
  readonly id: string;
  readonly name: string;
  readonly code: string;
  readonly description: string | null;
  readonly isActive: boolean;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface RoleRequest {
  readonly name: string;
  readonly code: string;
  readonly description: string | null;
}

export interface Permission {
  readonly code: string;
  readonly name: string;
  readonly module: string;
  readonly description: string | null;
  readonly granted: boolean;
}

/**
 * Roles are global (no Branch/Department scope). Permission assignment
 * (CRM-113) and user-role assignment (CRM-111) are out of scope here — this
 * service only covers create/view/edit/list/activate/deactivate.
 */
@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Promise<PagedResult<Role>> {
    return firstValueFrom(
      this.http.get<PagedResult<Role>>('/api/v1/roles', {
        params: { page, pageSize },
      }),
    );
  }

  get(id: string): Promise<Role> {
    return firstValueFrom(this.http.get<Role>(`/api/v1/roles/${id}`));
  }

  create(request: RoleRequest): Promise<Role> {
    return firstValueFrom(this.http.post<Role>('/api/v1/roles', request));
  }

  update(id: string, request: RoleRequest): Promise<Role> {
    return firstValueFrom(this.http.put<Role>(`/api/v1/roles/${id}`, request));
  }

  activate(id: string): Promise<Role> {
    return firstValueFrom(this.http.post<Role>(`/api/v1/roles/${id}/activate`, {}));
  }

  deactivate(id: string): Promise<Role> {
    return firstValueFrom(this.http.post<Role>(`/api/v1/roles/${id}/deactivate`, {}));
  }

  permissions(id: string): Promise<readonly Permission[]> {
    return firstValueFrom(this.http.get<readonly Permission[]>(`/api/v1/roles/${id}/permissions`));
  }

  replacePermissions(id: string, permissionCodes: readonly string[]): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/v1/roles/${id}/permissions`, { permissionCodes }),
    );
  }
}
