import { CardModule } from 'primeng/card';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TaskListQuery, TasksService } from './tasks.service';
import { AgentTask, AgentTaskStatus } from '../task-create/task-create.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

const STATUS_OPTIONS: readonly AgentTaskStatus[] = ['Open', 'Completed'];

/** Due-range presets (CRM-143). Computed client-side into `dueBefore`/`dueAfter`. */
type DueFilter = 'overdue' | 'dueToday' | 'dueThisWeek';

const DUE_FILTER_OPTIONS: readonly DueFilter[] = ['overdue', 'dueToday', 'dueThisWeek'];

@Component({
  selector: 'crm-task-list',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    DatePipe,
    FormsModule,
    RouterLink,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './task-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskList {
  private readonly tasksService = inject(TasksService);
  protected readonly localization = inject(LocalizationService);

  readonly tasks = signal<AgentTask[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);

  readonly search = signal('');
  readonly status = signal<AgentTaskStatus | null>(null);
  readonly dueFilter = signal<DueFilter | null>(null);
  readonly statusOptions: SelectOption[] = STATUS_OPTIONS.map((value) => ({
    label: value,
    value,
  }));
  readonly dueFilterOptions: SelectOption[] = DUE_FILTER_OPTIONS.map((value) => ({
    label: value,
    value,
  }));
  readonly pageSize = 20;

  protected statusLabel(status: AgentTaskStatus): string {
    return this.localization.translate(`tasks.statuses.${status}` as TranslationKey);
  }

  protected dueFilterLabel(value: DueFilter): string {
    return this.localization.translate(`tasks.dueFilter.${value}` as TranslationKey);
  }

  /** Translates a due-range preset into the concrete `dueBefore`/`dueAfter` bounds the API expects. */
  private resolveDueRange(filter: DueFilter | null): { dueBefore?: string; dueAfter?: string } {
    if (!filter) {
      return {};
    }
    const now = new Date();
    if (filter === 'overdue') {
      return { dueBefore: now.toISOString() };
    }
    if (filter === 'dueToday') {
      const endOfDay = new Date(now);
      endOfDay.setHours(23, 59, 59, 999);
      return { dueAfter: now.toISOString(), dueBefore: endOfDay.toISOString() };
    }
    const endOfWeek = new Date(now);
    endOfWeek.setDate(endOfWeek.getDate() + 7);
    return { dueAfter: now.toISOString(), dueBefore: endOfWeek.toISOString() };
  }

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const { dueBefore, dueAfter } = this.resolveDueRange(this.dueFilter());
      const query: TaskListQuery = {
        search: this.search() || undefined,
        statuses: this.status() ? [this.status()!] : undefined,
        dueBefore,
        dueAfter,
      };
      const result = await this.tasksService.list(query, page, this.pageSize);
      this.tasks.set([...result.items]);
      this.totalRecords.set(result.totalCount);
    } finally {
      this.loading.set(false);
    }
  }

  onLazyLoad(event: TableLazyLoadEvent): void {
    const first = event.first ?? 0;
    const rows = event.rows ?? this.pageSize;
    void this.load(Math.floor(first / rows) + 1);
  }

  onFilter(): void {
    void this.load(1);
  }
}
