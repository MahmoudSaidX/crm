import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Ticket, TicketChannel, TicketStatus } from '../ticket-create/ticket-create.service';

export type TicketSortBy = 'TicketNumber' | 'CreatedAtUtc';
export type SortDirection = 'Asc' | 'Desc';

export interface TicketListQuery {
  readonly search?: string;
  readonly statuses?: readonly TicketStatus[];
  readonly categoryIds?: readonly string[];
  readonly priorityIds?: readonly string[];
  readonly assigneeIds?: readonly string[];
  readonly departmentIds?: readonly string[];
  readonly branchIds?: readonly string[];
  readonly channels?: readonly TicketChannel[];
  readonly sortBy?: TicketSortBy;
  readonly sortDirection?: SortDirection;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

/**
 * Ticket browse/search/list (CRM-134, fourth story of the Ticket Management
 * epic CRM-130). Detail view is CRM-135, not built here.
 */
@Injectable({ providedIn: 'root' })
export class TicketsService {
  private readonly http = inject(HttpClient);

  list(query: TicketListQuery, page: number, pageSize: number): Promise<PagedResult<Ticket>> {
    let params: Record<string, string | readonly string[]> = {
      page: String(page),
      pageSize: String(pageSize),
    };
    if (query.search) {
      params = { ...params, search: query.search };
    }
    if (query.statuses?.length) {
      params = { ...params, statuses: query.statuses };
    }
    if (query.categoryIds?.length) {
      params = { ...params, categoryIds: query.categoryIds };
    }
    if (query.priorityIds?.length) {
      params = { ...params, priorityIds: query.priorityIds };
    }
    if (query.assigneeIds?.length) {
      params = { ...params, assigneeIds: query.assigneeIds };
    }
    if (query.departmentIds?.length) {
      params = { ...params, departmentIds: query.departmentIds };
    }
    if (query.branchIds?.length) {
      params = { ...params, branchIds: query.branchIds };
    }
    if (query.channels?.length) {
      params = { ...params, channels: query.channels };
    }
    if (query.sortBy) {
      params = { ...params, sortBy: query.sortBy };
    }
    if (query.sortDirection) {
      params = { ...params, sortDirection: query.sortDirection };
    }
    return firstValueFrom(this.http.get<PagedResult<Ticket>>('/api/v1/tickets', { params }));
  }
}
