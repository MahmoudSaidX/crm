import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface Branch {
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

export interface BranchRequest {
  readonly code: string;
  readonly arabicName: string;
  readonly englishName: string;
  readonly description: string | null;
}

/**
 * Branches are organizational structure (not a security role). Staff
 * membership (CRM-111) and ticket/customer scoping consumption are out of
 * scope here — this service only covers create/view/edit/list/activate/deactivate.
 */
@Injectable({ providedIn: 'root' })
export class BranchesService {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Promise<PagedResult<Branch>> {
    return firstValueFrom(
      this.http.get<PagedResult<Branch>>('/api/v1/branches', {
        params: { page, pageSize },
      }),
    );
  }

  get(id: string): Promise<Branch> {
    return firstValueFrom(this.http.get<Branch>(`/api/v1/branches/${id}`));
  }

  create(request: BranchRequest): Promise<Branch> {
    return firstValueFrom(this.http.post<Branch>('/api/v1/branches', request));
  }

  update(id: string, request: BranchRequest): Promise<Branch> {
    return firstValueFrom(this.http.put<Branch>(`/api/v1/branches/${id}`, request));
  }

  activate(id: string): Promise<Branch> {
    return firstValueFrom(this.http.post<Branch>(`/api/v1/branches/${id}/activate`, {}));
  }

  deactivate(id: string): Promise<Branch> {
    return firstValueFrom(this.http.post<Branch>(`/api/v1/branches/${id}/deactivate`, {}));
  }
}
