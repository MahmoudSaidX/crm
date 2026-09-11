import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { AgentTaskVersionedActionRequest, TasksService } from './tasks.service';
import { AgentTask, AgentTaskStatus } from '../task-create/task-create.service';
import { AuthorizationState } from '../auth/authorization.state';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { DetailGrid, PageContainer, PageHeader, StatePanel } from '@squad-crm/shared-ui';

/**
 * Read-only agent task detail plus the Complete/Reopen actions (CRM-143).
 * Follows the `ticket-detail` precedent, scaled down to what this story's
 * scope actually needs (no assignment/escalation/status-transition/history —
 * those belong to Ticket Management, not this simpler task model).
 *
 * The linked ticket/customer (when present) render as a `routerLink` only —
 * never fetched through this screen — because linking a task to a ticket or
 * customer does not grant access to that resource (BR). A caller without
 * access to the linked resource simply sees a link that its own guard/read
 * will reject.
 *
 * Complete/Reopen are gated on the `tasks.complete` permission. Ownership
 * beyond that is NOT checked client-side: no client-exposed "current user
 * id" pattern exists anywhere in this app today (`AuthorizationState` only
 * carries permission codes, and `/api/v1/authorization/me` returns no user
 * id) — see the story's escalation for the create-form linking pattern,
 * which surfaced the same gap. The backend remains authoritative and a
 * non-owner's action attempt is surfaced as a 403 explaining that the action
 * is no longer available, same as any other authorization failure.
 *
 * A 409 (stale version) is surfaced as "reload and try again" per BR — the
 * screen never retries with a version it already knows is stale.
 */
@Component({
  selector: 'crm-task-detail',
  imports: [
    RouterLink,
    DatePipe,
    ButtonModule,
    MessageModule,
    TagModule,
    CardModule,
    PageContainer,
    PageHeader,
    DetailGrid,
    StatePanel,
  ],
  templateUrl: './task-detail.html',
  styleUrl: './task-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskDetail {
  private readonly tasksService = inject(TasksService);
  private readonly route = inject(ActivatedRoute);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);

  readonly task = signal<AgentTask | null>(null);
  readonly notFound = signal(false);

  readonly submittingAction = signal(false);
  readonly actionErrorKey = signal<TranslationKey | null>(null);

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      void this.load(id);
    }
  }

  protected statusLabel(status: AgentTaskStatus): string {
    return this.localization.translate(`tasks.statuses.${status}` as TranslationKey);
  }

  async complete(): Promise<void> {
    await this.submitAction((id, request) => this.tasksService.complete(id, request));
  }

  async reopen(): Promise<void> {
    await this.submitAction((id, request) => this.tasksService.reopen(id, request));
  }

  private async submitAction(
    action: (id: string, request: AgentTaskVersionedActionRequest) => Promise<AgentTask>,
  ): Promise<void> {
    const task = this.task();
    if (!task || this.submittingAction()) {
      return;
    }

    this.submittingAction.set(true);
    this.actionErrorKey.set(null);
    try {
      await action(task.id, { version: task.version });
    } catch (error) {
      const status = error instanceof HttpErrorResponse ? error.status : 0;
      this.actionErrorKey.set(
        status === 409
          ? 'tasks.detail.errors.staleVersion'
          : status === 403
            ? 'tasks.detail.errors.forbidden'
            : 'tasks.detail.errors.failed',
      );
      this.submittingAction.set(false);
      return;
    }

    this.submittingAction.set(false);
    // Reload: the new status/completedAtUtc/version are all server-decided.
    await this.load(task.id);
  }

  private async load(id: string): Promise<void> {
    try {
      const task = await this.tasksService.get(id);
      this.task.set(task);
      this.notFound.set(false);
    } catch {
      this.notFound.set(true);
    }
  }
}
