import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * Agent task lifecycle status (CRM-143). Reopen is an explicitly supported
 * transition back to `Open` (BR), so this is not a one-way status.
 */
export type AgentTaskStatus = 'Open' | 'Completed';

/**
 * Agent task projection (CRM-143). The backend returns this same shape from
 * every endpoint (create/get/list-item/update/complete/reopen), so unlike
 * `Ticket`/`TicketDetail` there is no separate list-vs-detail projection.
 *
 * `ticketId`/`customerId` are link-only: linking a task to a ticket or
 * customer does not grant access to that resource (BR) — the detail screen
 * navigates to it via `routerLink` rather than fetching it through the task.
 */
export interface AgentTask {
  readonly id: string;
  readonly title: string;
  readonly details: string | null;
  readonly ownerUserId: string;
  readonly ticketId: string | null;
  readonly customerId: string | null;
  readonly dueAtUtc: string | null;
  readonly status: AgentTaskStatus;
  readonly completedAtUtc: string | null;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string | null;
  readonly version: number;
}

export interface CreateAgentTaskRequest {
  readonly title: string;
  readonly details: string | null;
  readonly ownerUserId: string | null;
  readonly ticketId: string | null;
  readonly customerId: string | null;
  readonly dueAtUtc: string | null;
}

/**
 * Agent task creation (CRM-143), the sibling of `TicketCreateService`. Only a
 * create call — browse/mine/detail live in `tasks/tasks.service.ts`.
 */
@Injectable({ providedIn: 'root' })
export class TaskCreateService {
  private readonly http = inject(HttpClient);

  create(request: CreateAgentTaskRequest): Promise<AgentTask> {
    return firstValueFrom(this.http.post<AgentTask>('/api/v1/tasks', request));
  }
}
