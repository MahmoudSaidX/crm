import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AgentTask, AgentTaskStatus } from '../task-create/task-create.service';

export type TaskSortBy = 'DueAtUtc' | 'CreatedAtUtc';
export type SortDirection = 'Asc' | 'Desc';

export interface TaskListQuery {
  readonly search?: string;
  readonly statuses?: readonly AgentTaskStatus[];
  readonly dueBefore?: string;
  readonly dueAfter?: string;

  /**
   * Restrict the result to the signed-in agent's own tasks (CRM-143). No
   * owner id is sent: the backend resolves the caller from the authenticated
   * principal, so the view cannot be pointed at another agent's tasks — same
   * pattern as `TicketListQuery.assignedToMe`.
   */
  readonly myTasksOnly?: boolean;
  readonly sortBy?: TaskSortBy;
  readonly sortDirection?: SortDirection;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

/**
 * `version` is the task version the screen last read: the backend rejects a
 * stale value with 409 rather than overwriting a newer change (BR — must be
 * surfaced as "reload and try again", never silently retried).
 */
export interface AgentTaskVersionedActionRequest {
  readonly version: number;
}

/**
 * Sets or reschedules the task's single reminder (CRM-144). `reminderAtUtc`
 * is a UTC ISO instant: the picker collects a local wall-clock time and
 * `Date.toISOString()` converts it, so the server never has to guess a
 * timezone. Clearing is the separate `clearReminder` call rather than a null
 * here, so "clear" can never be an accidental omission.
 */
export interface SetAgentTaskReminderRequest {
  readonly reminderAtUtc: string;
  readonly version: number;
}

/**
 * Agent task browse/search/list, "My Tasks" and detail (CRM-143).
 */
@Injectable({ providedIn: 'root' })
export class TasksService {
  private readonly http = inject(HttpClient);

  list(query: TaskListQuery, page: number, pageSize: number): Promise<PagedResult<AgentTask>> {
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
    if (query.dueBefore) {
      params = { ...params, dueBefore: query.dueBefore };
    }
    if (query.dueAfter) {
      params = { ...params, dueAfter: query.dueAfter };
    }
    if (query.myTasksOnly) {
      params = { ...params, myTasksOnly: 'true' };
    }
    if (query.sortBy) {
      params = { ...params, sortBy: query.sortBy };
    }
    if (query.sortDirection) {
      params = { ...params, sortDirection: query.sortDirection };
    }
    return firstValueFrom(this.http.get<PagedResult<AgentTask>>('/api/v1/tasks', { params }));
  }

  get(id: string): Promise<AgentTask> {
    return firstValueFrom(this.http.get<AgentTask>(`/api/v1/tasks/${id}`));
  }

  complete(id: string, request: AgentTaskVersionedActionRequest): Promise<AgentTask> {
    return firstValueFrom(this.http.post<AgentTask>(`/api/v1/tasks/${id}/complete`, request));
  }

  reopen(id: string, request: AgentTaskVersionedActionRequest): Promise<AgentTask> {
    return firstValueFrom(this.http.post<AgentTask>(`/api/v1/tasks/${id}/reopen`, request));
  }

  setReminder(id: string, request: SetAgentTaskReminderRequest): Promise<AgentTask> {
    return firstValueFrom(this.http.put<AgentTask>(`/api/v1/tasks/${id}/reminder`, request));
  }

  /**
   * The version travels as a query parameter rather than a DELETE body: a
   * DELETE body is dropped by some proxies, and the concurrency check is
   * exactly the thing that must not be silently lost.
   */
  clearReminder(id: string, request: AgentTaskVersionedActionRequest): Promise<AgentTask> {
    return firstValueFrom(
      this.http.delete<AgentTask>(`/api/v1/tasks/${id}/reminder`, {
        params: { version: String(request.version) },
      }),
    );
  }
}
