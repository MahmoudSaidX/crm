import { CardModule } from 'primeng/card';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ParamMap, Params, RouterLink } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TaskListQuery, TasksService } from './tasks.service';
import { AgentTask, AgentTaskStatus } from '../task-create/task-create.service';
import {
  LocalizationService,
  TranslationKey,
  injectListUrlState,
  paginationParams,
  readEnum,
  readPagination,
  readText,
  toQueryParams,
} from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

const STATUS_OPTIONS: readonly AgentTaskStatus[] = ['Open', 'Completed'];

/** Due-range presets (CRM-143). Computed client-side into `dueBefore`/`dueAfter`. */
type DueFilter = 'overdue' | 'dueToday' | 'dueThisWeek';

const DUE_FILTER_OPTIONS: readonly DueFilter[] = ['overdue', 'dueToday', 'dueThisWeek'];

/**
 * URL-addressable task-list state. The due filter travels as its *preset name*
 * rather than the resolved `dueBefore`/`dueAfter` instants: "overdue" must still
 * mean overdue when the link is opened tomorrow, and a pair of frozen timestamps
 * would not.
 */
interface TaskListState {
  readonly page: number;
  readonly pageSize: number;
  readonly search: string;
  readonly status: AgentTaskStatus | null;
  readonly due: DueFilter | null;
}

function parseState(params: ParamMap): TaskListState {
  return {
    ...readPagination(params),
    search: readText(params, 'search'),
    status: readEnum(params, 'status', STATUS_OPTIONS),
    due: readEnum(params, 'due', DUE_FILTER_OPTIONS),
  };
}

function stateToParams(state: TaskListState): Params {
  return {
    ...paginationParams(state),
    ...toQueryParams({ search: state.search, status: state.status, due: state.due }),
  };
}

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

  /** URL-owned list state; the signals above remain the submit-based draft. */
  private readonly urlState = injectListUrlState<TaskListState>(parseState, stateToParams);

  protected readonly page = computed(() => this.urlState.state().page);
  protected readonly pageSize = computed(() => this.urlState.state().pageSize);
  protected readonly first = computed(() => (this.page() - 1) * this.pageSize());

  constructor() {
    effect(() => {
      const state = this.urlState.state();
      this.search.set(state.search);
      this.status.set(state.status);
      this.dueFilter.set(state.due);
      void this.load(state);
    });
  }

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

  async load(state: TaskListState): Promise<void> {
    this.loading.set(true);
    try {
      const { dueBefore, dueAfter } = this.resolveDueRange(state.due);
      const query: TaskListQuery = {
        search: state.search || undefined,
        statuses: state.status ? [state.status] : undefined,
        dueBefore,
        dueAfter,
      };
      const result = await this.tasksService.list(query, state.page, state.pageSize);
      this.tasks.set([...result.items]);
      this.totalRecords.set(result.totalCount);
    } finally {
      this.loading.set(false);
    }
  }

  onPage(event: TablePageEvent): void {
    const rows = event.rows ?? this.pageSize();
    const page = Math.floor((event.first ?? 0) / rows) + 1;
    if (page !== this.page()) {
      this.urlState.setPage(page);
    }
  }

  onFilter(): void {
    this.urlState.patch({
      search: this.search(),
      status: this.status(),
      due: this.dueFilter(),
    });
  }
}
