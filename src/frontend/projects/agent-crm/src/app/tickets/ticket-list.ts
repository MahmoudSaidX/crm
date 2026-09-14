import { CardModule } from 'primeng/card';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SortDirection, TicketListQuery, TicketSortBy, TicketsService } from './tickets.service';
import { Ticket, TicketChannel, TicketStatus } from '../ticket-create/ticket-create.service';
import { TicketCategoriesService } from '../ticket-categories/ticket-categories.service';
import { TicketPrioritiesService } from '../ticket-priorities/ticket-priorities.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import {
  LocalizationService,
  TranslationKey,
  injectListUrlState,
  paginationParams,
  readEnum,
  readGuid,
  readPagination,
  readText,
  toQueryParams,
} from '@squad-crm/platform';
import { ParamMap, Params } from '@angular/router';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

/** Server-supported sort fields. Anything else in the URL is ignored, not passed on. */
const SORT_FIELDS = ['TicketNumber', 'CreatedAtUtc', 'UpdatedAtUtc'] as const;
const SORT_DIRECTIONS = ['Asc', 'Desc'] as const;

const DEFAULT_SORT: TicketSortBy = 'TicketNumber';
const DEFAULT_SORT_DIRECTION: SortDirection = 'Asc';

/**
 * The list state this screen puts in the URL. Single-select filters are
 * singular here and become the backend's plural array parameter at the API
 * boundary — the wire contract is not renamed to match the UI.
 */
interface TicketListState {
  readonly page: number;
  readonly pageSize: number;
  readonly search: string;
  readonly categoryId: string | null;
  readonly priorityId: string | null;
  readonly departmentId: string | null;
  readonly branchId: string | null;
  readonly channel: TicketChannel | null;
  readonly sort: TicketSortBy;
  readonly dir: SortDirection;
}

function parseState(params: ParamMap): TicketListState {
  return {
    ...readPagination(params),
    search: readText(params, 'search'),
    categoryId: readGuid(params, 'categoryId'),
    priorityId: readGuid(params, 'priorityId'),
    departmentId: readGuid(params, 'departmentId'),
    branchId: readGuid(params, 'branchId'),
    channel: readEnum(params, 'channel', CHANNEL_OPTIONS),
    sort: readEnum(params, 'sort', SORT_FIELDS) ?? DEFAULT_SORT,
    dir: readEnum(params, 'dir', SORT_DIRECTIONS) ?? DEFAULT_SORT_DIRECTION,
  };
}

function stateToParams(state: TicketListState): Params {
  return {
    ...paginationParams(state),
    ...toQueryParams(
      {
        search: state.search,
        categoryId: state.categoryId,
        priorityId: state.priorityId,
        departmentId: state.departmentId,
        branchId: state.branchId,
        channel: state.channel,
        sort: state.sort,
        dir: state.dir,
      },
      { sort: DEFAULT_SORT, dir: DEFAULT_SORT_DIRECTION },
    ),
  };
}

const CHANNEL_OPTIONS: readonly TicketChannel[] = [
  'Agent',
  'Portal',
  'Email',
  'WhatsApp',
  'LiveChat',
  'SMS',
  'WebForm',
];

@Component({
  selector: 'crm-ticket-list',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    FormsModule,
    RouterLink,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './ticket-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketList {
  private readonly ticketsService = inject(TicketsService);
  private readonly ticketCategoriesService = inject(TicketCategoriesService);
  private readonly ticketPrioritiesService = inject(TicketPrioritiesService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly branchesService = inject(BranchesService);
  protected readonly localization = inject(LocalizationService);

  readonly tickets = signal<Ticket[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly search = signal('');
  readonly categoryId = signal<string | null>(null);
  readonly priorityId = signal<string | null>(null);
  readonly departmentId = signal<string | null>(null);
  readonly branchId = signal<string | null>(null);
  readonly channel = signal<TicketChannel | null>(null);
  readonly categoryOptions = signal<SelectOption[]>([]);
  readonly priorityOptions = signal<SelectOption[]>([]);
  readonly departmentOptions = signal<SelectOption[]>([]);
  readonly branchOptions = signal<SelectOption[]>([]);
  readonly channelOptions: SelectOption[] = CHANNEL_OPTIONS.map((value) => ({
    label: value,
    value,
  }));

  /**
   * The URL owns this screen's list state. The signals above are only the
   * *draft* bound to the inputs — the search box and selects keep their
   * existing submit-based UX — and `onFilter()` is what promotes a draft into
   * the URL, from where the load is driven.
   */
  private readonly urlState = injectListUrlState<TicketListState>(parseState, stateToParams);

  protected readonly page = computed(() => this.urlState.state().page);
  protected readonly pageSize = computed(() => this.urlState.state().pageSize);
  protected readonly first = computed(() => (this.page() - 1) * this.pageSize());

  /** Localized lifecycle status label (CRM-137). */
  protected statusLabel(status: TicketStatus): string {
    return this.localization.translate(`tickets.statuses.${status}` as TranslationKey);
  }

  constructor() {
    void this.loadFilterOptions();

    // Seeds the inputs from the URL — on first paint, on a deep link, and on
    // Back/Forward, which must restore the controls as well as the results.
    effect(() => {
      const state = this.urlState.state();
      this.search.set(state.search);
      this.categoryId.set(state.categoryId);
      this.priorityId.set(state.priorityId);
      this.departmentId.set(state.departmentId);
      this.branchId.set(state.branchId);
      this.channel.set(state.channel);
      void this.load(state);
    });
  }

  private async loadFilterOptions(): Promise<void> {
    const [categories, priorities, departments, branches] = await Promise.all([
      this.ticketCategoriesService.list(1, 200),
      this.ticketPrioritiesService.list(1, 200),
      this.departmentsService.list(1, 200),
      this.branchesService.list(1, 200),
    ]);
    this.categoryOptions.set(
      categories.items
        .filter((category) => category.isActive)
        .map((category) => ({ label: category.englishName, value: category.id })),
    );
    this.priorityOptions.set(
      priorities.items
        .filter((priority) => priority.isActive)
        .map((priority) => ({ label: priority.englishName, value: priority.id })),
    );
    this.departmentOptions.set(
      departments.items.map((department) => ({
        label: department.englishName,
        value: department.id,
      })),
    );
    this.branchOptions.set(
      branches.items.map((branch) => ({ label: branch.englishName, value: branch.id })),
    );
  }

  async load(state: TicketListState): Promise<void> {
    this.loading.set(true);
    try {
      const query: TicketListQuery = {
        search: state.search || undefined,
        categoryIds: state.categoryId ? [state.categoryId] : undefined,
        priorityIds: state.priorityId ? [state.priorityId] : undefined,
        departmentIds: state.departmentId ? [state.departmentId] : undefined,
        branchIds: state.branchId ? [state.branchId] : undefined,
        channels: state.channel ? [state.channel] : undefined,
        sortBy: state.sort,
        sortDirection: state.dir,
      };
      const result = await this.ticketsService.list(query, state.page, state.pageSize);
      this.tickets.set([...result.items]);
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

  /** Search or filter submitted: the URL changes, which resets to page 1 and reloads. */
  onFilter(): void {
    this.urlState.patch({
      search: this.search(),
      categoryId: this.categoryId(),
      priorityId: this.priorityId(),
      departmentId: this.departmentId(),
      branchId: this.branchId(),
      channel: this.channel(),
    });
  }
}
