import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface Department {
  readonly id: string;
  readonly code: string;
  readonly arabicName: string;
  readonly englishName: string;
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

export interface DepartmentRequest {
  readonly code: string;
  readonly arabicName: string;
  readonly englishName: string;
  readonly description: string | null;
}

/**
 * Departments are organizational structure (not a security role). Staff
 * membership (CRM-111) and ticket/customer scoping consumption are out of
 * scope here — this service only covers create/view/edit/list/activate/deactivate.
 */
@Injectable({ providedIn: 'root' })
export class DepartmentsService {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Promise<PagedResult<Department>> {
    return firstValueFrom(
      this.http.get<PagedResult<Department>>('/api/v1/departments', {
        params: { page, pageSize },
      }),
    );
  }

  get(id: string): Promise<Department> {
    return firstValueFrom(this.http.get<Department>(`/api/v1/departments/${id}`));
  }

  create(request: DepartmentRequest): Promise<Department> {
    return firstValueFrom(this.http.post<Department>('/api/v1/departments', request));
  }

  update(id: string, request: DepartmentRequest): Promise<Department> {
    return firstValueFrom(this.http.put<Department>(`/api/v1/departments/${id}`, request));
  }

  activate(id: string): Promise<Department> {
    return firstValueFrom(this.http.post<Department>(`/api/v1/departments/${id}/activate`, {}));
  }

  deactivate(id: string): Promise<Department> {
    return firstValueFrom(this.http.post<Department>(`/api/v1/departments/${id}/deactivate`, {}));
  }
}
