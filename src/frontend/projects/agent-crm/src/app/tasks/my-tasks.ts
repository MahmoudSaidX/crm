import { CardModule } from 'primeng/card';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SortDirection, TaskListQuery, TaskSortBy, TasksService } from './tasks.service';
import { AgentTask, AgentTaskStatus } from '../task-create/task-create.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

const STATUS_OPTIONS: readonly AgentTaskStatus[] = ['Open', 'Completed'];

interface SortOption {
  readonly sortBy: TaskSortBy;
  readonly sortDirection: SortDirection;
}

const SORT_OPTIONS: Readonly<Record<string, SortOption>> = {
  dueAsc: { sortBy: 'DueAtUtc', sortDirection: 'Asc' },
  dueDesc: { sortBy: 'DueAtUtc', sortDirection: 'Desc' },
  createdDesc: { sortBy: 'CreatedAtUtc', sortDirection: 'Desc' },
};

/**
 * The signed-in agent's own task list (CRM-143).
 *
 * The screen never sends an owner id — it sets `myTasksOnly`, and the
 * backend resolves the caller from the authenticated principal, mirroring
 * `MyTickets`/`assignedToMe` (CRM-141).
 */
@Component({
  selector: 'crm-my-tasks',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    DatePipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './my-tasks.html',
  styleUrl: './my-tasks.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyTasks {
  private readonly tasksService = inject(TasksService);
  protected readonly localization = inject(LocalizationService);

  readonly tasks = signal<AgentTask[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);

  /** Set when the list could not be loaded at all — distinct from an empty list. */
  readonly loadFailed = signal(false);

  readonly search = signal('');
  readonly status = signal<AgentTaskStatus | null>(null);
  readonly sort = signal<string>('dueAsc');
  readonly statusOptions: SelectOption[] = STATUS_OPTIONS.map((value) => ({
    label: value,
    value,
  }));
  readonly sortOptions: SelectOption[] = Object.keys(SORT_OPTIONS).map((value) => ({
    label: value,
    value,
  }));
  readonly pageSize = 20;
  private page = 1;

  protected statusLabel(status: AgentTaskStatus): string {
    return this.localization.translate(`tasks.statuses.${status}` as TranslationKey);
  }

  protected sortLabel(value: string): string {
    return this.localization.translate(`tasks.mine.sort.${value}` as TranslationKey);
  }

  async load(page = 1): Promise<void> {
    this.page = page;
    this.loading.set(true);
    const sort = SORT_OPTIONS[this.sort()] ?? SORT_OPTIONS['dueAsc'];
    try {
      const query: TaskListQuery = {
        myTasksOnly: true,
        search: this.search() || undefined,
        statuses: this.status() ? [this.status()!] : undefined,
        sortBy: sort.sortBy,
        sortDirection: sort.sortDirection,
      };
      const result = await this.tasksService.list(query, page, this.pageSize);
      this.tasks.set([...result.items]);
      this.totalRecords.set(result.totalCount);
      this.loadFailed.set(false);
    } catch {
      this.tasks.set([]);
      this.totalRecords.set(0);
      this.loadFailed.set(true);
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

  /** Re-reads the current page from the server — persisted state is authoritative. */
  onRefresh(): void {
    void this.load(this.page);
  }
}
