import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface TicketPriority {
  readonly id: string;
  readonly code: string;
  readonly arabicName: string;
  readonly englishName: string;
  readonly rank: number;
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

export interface TicketPriorityRequest {
  readonly code: string;
  readonly arabicName: string;
  readonly englishName: string;
  readonly rank: number;
  readonly description: string | null;
}

/**
 * Ticket priorities (CRM-132, second story of the Ticket Management epic
 * CRM-130). There is no Ticket entity yet — this service only covers
 * create/view/edit/list/activate/deactivate of the priority catalog.
 */
@Injectable({ providedIn: 'root' })
export class TicketPrioritiesService {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Promise<PagedResult<TicketPriority>> {
    return firstValueFrom(
      this.http.get<PagedResult<TicketPriority>>('/api/v1/ticket-priorities', {
        params: { page, pageSize },
      }),
    );
  }

  get(id: string): Promise<TicketPriority> {
    return firstValueFrom(this.http.get<TicketPriority>(`/api/v1/ticket-priorities/${id}`));
  }

  create(request: TicketPriorityRequest): Promise<TicketPriority> {
    return firstValueFrom(this.http.post<TicketPriority>('/api/v1/ticket-priorities', request));
  }

  update(id: string, request: TicketPriorityRequest): Promise<TicketPriority> {
    return firstValueFrom(
      this.http.put<TicketPriority>(`/api/v1/ticket-priorities/${id}`, request),
    );
  }

  activate(id: string): Promise<TicketPriority> {
    return firstValueFrom(
      this.http.post<TicketPriority>(`/api/v1/ticket-priorities/${id}/activate`, {}),
    );
  }

  deactivate(id: string): Promise<TicketPriority> {
    return firstValueFrom(
      this.http.post<TicketPriority>(`/api/v1/ticket-priorities/${id}/deactivate`, {}),
    );
  }
}
