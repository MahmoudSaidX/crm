import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface TicketCategory {
  readonly id: string;
  readonly code: string;
  readonly arabicName: string;
  readonly englishName: string;
  readonly defaultDepartmentId: string | null;
  readonly sortOrder: number;
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

export interface TicketCategoryRequest {
  readonly code: string;
  readonly arabicName: string;
  readonly englishName: string;
  readonly defaultDepartmentId: string | null;
  readonly sortOrder: number;
}

/**
 * Ticket categories (CRM-131, first story of the Ticket Management epic
 * CRM-130). There is no Ticket entity yet — this service only covers
 * create/view/edit/list/activate/deactivate of the category catalog.
 */
@Injectable({ providedIn: 'root' })
export class TicketCategoriesService {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Promise<PagedResult<TicketCategory>> {
    return firstValueFrom(
      this.http.get<PagedResult<TicketCategory>>('/api/v1/ticket-categories', {
        params: { page, pageSize },
      }),
    );
  }

  get(id: string): Promise<TicketCategory> {
    return firstValueFrom(this.http.get<TicketCategory>(`/api/v1/ticket-categories/${id}`));
  }

  create(request: TicketCategoryRequest): Promise<TicketCategory> {
    return firstValueFrom(this.http.post<TicketCategory>('/api/v1/ticket-categories', request));
  }

  update(id: string, request: TicketCategoryRequest): Promise<TicketCategory> {
    return firstValueFrom(
      this.http.put<TicketCategory>(`/api/v1/ticket-categories/${id}`, request),
    );
  }

  activate(id: string): Promise<TicketCategory> {
    return firstValueFrom(
      this.http.post<TicketCategory>(`/api/v1/ticket-categories/${id}/activate`, {}),
    );
  }

  deactivate(id: string): Promise<TicketCategory> {
    return firstValueFrom(
      this.http.post<TicketCategory>(`/api/v1/ticket-categories/${id}/deactivate`, {}),
    );
  }
}
