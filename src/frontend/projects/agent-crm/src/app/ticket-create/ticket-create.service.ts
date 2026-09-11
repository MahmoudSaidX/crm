import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * Ticket lifecycle statuses (CRM-137). Which transitions are available from a
 * given status is decided by the backend and reported per ticket
 * (`TicketDetail.allowedStatusTransitions`) — the client never hardcodes the
 * matrix.
 */
export type TicketStatus =
  'Open' | 'InProgress' | 'PendingCustomer' | 'PendingInternal' | 'Resolved' | 'Closed';

/**
 * Escalation target kinds (CRM-138). Only the two the backend can validate —
 * an active agent and an active department — are supported.
 */
export type TicketEscalationTargetType = 'Agent' | 'Department';

export type TicketChannel =
  'Agent' | 'Portal' | 'Email' | 'WhatsApp' | 'LiveChat' | 'SMS' | 'WebForm';

export interface Ticket {
  readonly id: string;
  readonly ticketNumber: string;
  readonly customerId: string;
  readonly subject: string;
  readonly description: string;
  readonly categoryId: string;
  readonly subcategoryId: string | null;
  readonly priorityId: string;
  readonly departmentId: string;
  readonly branchId: string;
  readonly status: TicketStatus;
  readonly channel: TicketChannel;
  readonly assignedAgentId: string | null;

  /**
   * Current escalation state (CRM-138), reported independently of `status` —
   * escalation is not a lifecycle status. Level 0 means not escalated, and the
   * target fields are then null.
   */
  readonly escalationLevel: number;
  readonly escalationTargetType: TicketEscalationTargetType | null;
  readonly escalationTargetId: string | null;
  readonly escalatedAtUtc: string | null;
  readonly createdAtUtc: string;
}

export interface TicketRequest {
  readonly customerId: string;
  readonly subject: string;
  readonly description: string;
  readonly categoryId: string;
  readonly subcategoryId: string | null;
  readonly priorityId: string;
  readonly departmentId: string;
  readonly branchId: string;
  readonly channel: TicketChannel;
  readonly assignedAgentId: string | null;
}

/**
 * Ticket creation (CRM-133, third story of the Ticket Management epic
 * CRM-130). Only a create form — browse/view/detail are CRM-134/135, not
 * built here.
 */
@Injectable({ providedIn: 'root' })
export class TicketCreateService {
  private readonly http = inject(HttpClient);

  create(request: TicketRequest): Promise<Ticket> {
    return firstValueFrom(this.http.post<Ticket>('/api/v1/tickets', request));
  }
}
