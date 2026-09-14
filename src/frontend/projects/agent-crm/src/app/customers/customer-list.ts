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
import { ParamMap, Params, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Customer, CustomerSortBy, CustomersService, SortDirection } from './customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import {
  LocalizationService,
  injectListUrlState,
  paginationParams,
  readEnum,
  readGuid,
  readPagination,
  readText,
  toQueryParams,
} from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';
import { AuthorizationState } from '../auth/authorization.state';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

/** Server-supported sort fields; anything else in the URL is ignored, not forwarded. */
const SORT_FIELDS = ['CustomerNumber', 'FirstName', 'LastName', 'CreatedAtUtc'] as const;
const SORT_DIRECTIONS = ['Asc', 'Desc'] as const;

const DEFAULT_SORT: CustomerSortBy = 'CustomerNumber';
const DEFAULT_SORT_DIRECTION: SortDirection = 'Asc';

interface CustomerListState {
  readonly page: number;
  readonly pageSize: number;
  readonly search: string;
  readonly departmentId: string | null;
  readonly branchId: string | null;
  readonly sort: CustomerSortBy;
  readonly dir: SortDirection;
}

function parseState(params: ParamMap): CustomerListState {
  return {
    ...readPagination(params),
    search: readText(params, 'search'),
    departmentId: readGuid(params, 'departmentId'),
    branchId: readGuid(params, 'branchId'),
    sort: readEnum(params, 'sort', SORT_FIELDS) ?? DEFAULT_SORT,
    dir: readEnum(params, 'dir', SORT_DIRECTIONS) ?? DEFAULT_SORT_DIRECTION,
  };
}

function stateToParams(state: CustomerListState): Params {
  return {
    ...paginationParams(state),
    ...toQueryParams(
      {
        search: state.search,
        departmentId: state.departmentId,
        branchId: state.branchId,
        sort: state.sort,
        dir: state.dir,
      },
      { sort: DEFAULT_SORT, dir: DEFAULT_SORT_DIRECTION },
    ),
  };
}

@Component({
  selector: 'crm-customer-list',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    RouterLink,
    FormsModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './customer-list.html',
  styleUrl: './customer-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerList {
  private readonly customersService = inject(CustomersService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly branchesService = inject(BranchesService);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);

  readonly customers = signal<Customer[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly search = signal('');
  readonly departmentId = signal<string | null>(null);
  readonly branchId = signal<string | null>(null);
  readonly departmentOptions = signal<SelectOption[]>([]);
  readonly branchOptions = signal<SelectOption[]>([]);

  /**
   * URL-owned list state. The signals above stay as the draft the inputs bind
   * to, preserving this screen's submit-on-Enter search; `onFilter()` promotes
   * the draft into the URL and the load follows from there.
   */
  private readonly urlState = injectListUrlState<CustomerListState>(parseState, stateToParams);

  protected readonly page = computed(() => this.urlState.state().page);
  protected readonly pageSize = computed(() => this.urlState.state().pageSize);
  protected readonly first = computed(() => (this.page() - 1) * this.pageSize());

  constructor() {
    void this.loadFilterOptions();

    effect(() => {
      const state = this.urlState.state();
      this.search.set(state.search);
      this.departmentId.set(state.departmentId);
      this.branchId.set(state.branchId);
      void this.load(state);
    });
  }

  private async loadFilterOptions(): Promise<void> {
    const [departments, branches] = await Promise.all([
      this.departmentsService.list(1, 200),
      this.branchesService.list(1, 200),
    ]);
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

  async load(state: CustomerListState): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.customersService.list(
        {
          search: state.search || undefined,
          departmentIds: state.departmentId ? [state.departmentId] : undefined,
          branchIds: state.branchId ? [state.branchId] : undefined,
          sortBy: state.sort,
          sortDirection: state.dir,
        },
        state.page,
        state.pageSize,
      );
      this.customers.set([...result.items]);
      this.totalRecords.set(result.totalCount);
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * The paginator's own event — deliberately not the table's `onLazyLoad`.
   * `onLazyLoad` also fires on init and whenever `[value]` is reassigned, and
   * each load assigns a fresh array, so the table would echo back `first: 0`
   * after every page change and bounce the URL to page 1.
   */
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
      departmentId: this.departmentId(),
      branchId: this.branchId(),
    });
  }

  openCustomer(customer: Customer): void {
    void this.router.navigate(['/customers', customer.id]);
  }
}
