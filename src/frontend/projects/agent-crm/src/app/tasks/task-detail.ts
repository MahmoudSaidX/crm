import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { FormsModule } from '@angular/forms';
import { DatePickerModule } from 'primeng/datepicker';
import { AgentTaskVersionedActionRequest, TasksService } from './tasks.service';
import {
  AgentTask,
  AgentTaskReminderStatus,
  AgentTaskStatus,
} from '../task-create/task-create.service';
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
 *
 * The Reminder section (CRM-144) sets/updates/clears the task's single
 * optional reminder. The picker works in the browser's own timezone and the
 * value is sent as a UTC instant, which is how the AC's "displayed in the
 * user's timezone, persisted/scheduled in UTC" split is satisfied without a
 * server-side per-user timezone model (none exists in the app today).
 */
@Component({
  selector: 'crm-task-detail',
  imports: [
    RouterLink,
    DatePipe,
    FormsModule,
    DatePickerModule,
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

  /** Bound to the reminder picker; seeded from the loaded task. */
  readonly reminderDraft = signal<Date | null>(null);
  readonly submittingReminder = signal(false);
  readonly reminderErrorKey = signal<TranslationKey | null>(null);

  /**
   * The picker must not offer an instant that the backend would reject as
   * already past. Captured per read rather than once at construction, so a
   * long-open screen does not keep an increasingly stale floor.
   */
  protected get minReminderDate(): Date {
    return new Date();
  }

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      void this.load(id);
    }
  }

  protected statusLabel(status: AgentTaskStatus): string {
    return this.localization.translate(`tasks.statuses.${status}` as TranslationKey);
  }

  protected reminderStatusLabel(status: AgentTaskReminderStatus): string {
    return this.localization.translate(
      `tasks.detail.reminder.statuses.${status}` as TranslationKey,
    );
  }

  /**
   * Sets or reschedules the reminder. Rescheduling needs no client-side
   * cancellation of the previous occurrence: the backend supersedes it by
   * minting a new reminder event id (AC "without duplicate alerts").
   */
  async saveReminder(): Promise<void> {
    const task = this.task();
    const draft = this.reminderDraft();
    if (!task || this.submittingReminder()) {
      return;
    }

    if (!draft) {
      this.reminderErrorKey.set('tasks.detail.reminder.errors.required');
      return;
    }

    this.submittingReminder.set(true);
    this.reminderErrorKey.set(null);
    try {
      await this.tasksService.setReminder(task.id, {
        // The picker yields a local wall-clock time; the server stores and
        // schedules the UTC instant it corresponds to.
        reminderAtUtc: draft.toISOString(),
        version: task.version,
      });
    } catch (error) {
      this.reminderErrorKey.set(this.reminderErrorFor(error));
      this.submittingReminder.set(false);
      return;
    }

    this.submittingReminder.set(false);
    await this.load(task.id);
  }

  async clearReminder(): Promise<void> {
    const task = this.task();
    if (!task || this.submittingReminder()) {
      return;
    }

    this.submittingReminder.set(true);
    this.reminderErrorKey.set(null);
    try {
      await this.tasksService.clearReminder(task.id, { version: task.version });
    } catch (error) {
      this.reminderErrorKey.set(this.reminderErrorFor(error));
      this.submittingReminder.set(false);
      return;
    }

    this.submittingReminder.set(false);
    await this.load(task.id);
  }

  /**
   * A 422 carries the specific reason in its ProblemDetails `code`, so the
   * agent is told which rule they hit rather than a generic failure.
   */
  private reminderErrorFor(error: unknown): TranslationKey {
    if (!(error instanceof HttpErrorResponse)) {
      return 'tasks.detail.errors.failed';
    }

    if (error.status === 409) {
      return 'tasks.detail.errors.staleVersion';
    }

    if (error.status === 403) {
      return 'tasks.detail.errors.forbidden';
    }

    if (error.status === 422) {
      const code: unknown = error.error?.code;
      if (code === 'tasks.reminder_in_past') {
        return 'tasks.detail.reminder.errors.inPast';
      }

      if (code === 'tasks.reminder_task_not_open') {
        return 'tasks.detail.reminder.errors.taskNotOpen';
      }
    }

    return 'tasks.detail.errors.failed';
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
      // Re-seed the picker from the server's answer, so the field always
      // reflects what is actually scheduled rather than an abandoned draft.
      this.reminderDraft.set(task.reminderAtUtc ? new Date(task.reminderAtUtc) : null);
      this.notFound.set(false);
    } catch {
      this.notFound.set(true);
    }
  }
}
