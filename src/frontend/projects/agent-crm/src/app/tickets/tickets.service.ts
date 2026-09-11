import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  Ticket,
  TicketChannel,
  TicketEscalationTargetType,
  TicketStatus,
} from '../ticket-create/ticket-create.service';

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
 * Ticket detail projection (CRM-135). Category/priority names come resolved
 * from the backend because both catalogs live inside the TicketManagement
 * module; a null name means the referenced row no longer exists at all, and
 * the `*IsActive` flags let the UI mark a value that was deactivated after the
 * ticket was created without hiding the historical label.
 *
 * Customer/department/branch labels are deliberately NOT part of this payload
 * — those modules own their own data and their own authorization, so the
 * detail screen resolves them through their own services.
 */
export interface TicketDetail {
  readonly id: string;
  readonly ticketNumber: string;
  readonly customerId: string;
  readonly subject: string;
  readonly description: string;
  readonly categoryId: string;
  readonly categoryArabicName: string | null;
  readonly categoryEnglishName: string | null;
  readonly categoryIsActive: boolean | null;
  readonly subcategoryId: string | null;
  readonly priorityId: string;
  readonly priorityArabicName: string | null;
  readonly priorityEnglishName: string | null;
  readonly priorityIsActive: boolean | null;
  readonly priorityRank: number | null;
  readonly departmentId: string;
  readonly branchId: string;
  readonly status: TicketStatus;
  readonly channel: TicketChannel;
  readonly assignedAgentId: string | null;

  /**
   * Current escalation state (CRM-138). Level 0 means the ticket is not
   * escalated; the target fields are then null.
   */
  readonly escalationLevel: number;
  readonly escalationTargetType: TicketEscalationTargetType | null;
  readonly escalationTargetId: string | null;
  readonly escalatedAtUtc: string | null;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string | null;
  readonly version: number;

  /**
   * Statuses this ticket may currently move to (CRM-137), reported by the
   * backend so the screen offers only valid actions. A UX hint only: the
   * transition endpoint re-validates every call.
   */
  readonly allowedStatusTransitions: readonly TicketStatus[];
}

/**
 * Assignment/reassignment command (CRM-136). `version` is the ticket version
 * the screen last read: the backend rejects a stale value instead of
 * overwriting a newer owner. `reason` is required by the backend only when an
 * existing owner is replaced.
 */
export interface AssignTicketRequest {
  readonly targetAgentId: string;
  readonly reason: string | null;
  readonly version: number;
}

/**
 * Lifecycle status-transition command (CRM-137). `version` is the ticket
 * version the screen last read; `reason` is required by the backend for close
 * and reopen transitions.
 */
export interface ChangeTicketStatusRequest {
  readonly targetStatus: TicketStatus;
  readonly reason: string | null;
  readonly version: number;
}

/**
 * Manual escalation command (CRM-138). `version` is the ticket version the
 * screen last read; `reason` is always required. The escalation LEVEL is not
 * sent — the backend derives it, so a client cannot corrupt the sequence.
 */
export interface EscalateTicketRequest {
  readonly targetType: TicketEscalationTargetType;
  readonly targetId: string;
  readonly reason: string;
  readonly version: number;
}

/**
 * Ticket browse/search/list (CRM-134), ticket detail (CRM-135), ticket
 * assignment (CRM-136), status lifecycle (CRM-137) and manual escalation
 * (CRM-138), all stories of the Ticket Management epic CRM-130.
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

  get(id: string): Promise<TicketDetail> {
    return firstValueFrom(this.http.get<TicketDetail>(`/api/v1/tickets/${id}`));
  }

  assign(id: string, request: AssignTicketRequest): Promise<Ticket> {
    return firstValueFrom(this.http.post<Ticket>(`/api/v1/tickets/${id}/assign`, request));
  }

  changeStatus(id: string, request: ChangeTicketStatusRequest): Promise<Ticket> {
    return firstValueFrom(this.http.post<Ticket>(`/api/v1/tickets/${id}/status`, request));
  }

  escalate(id: string, request: EscalateTicketRequest): Promise<Ticket> {
    return firstValueFrom(this.http.post<Ticket>(`/api/v1/tickets/${id}/escalate`, request));
  }
}
