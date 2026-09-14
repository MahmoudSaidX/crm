import { CardModule } from 'primeng/card';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TicketCategory, TicketCategoriesService } from './ticket-categories.service';
import { Department, DepartmentsService } from '../departments/departments.service';
import {
  LocalizationService,
  PagedListState,
  injectListUrlState,
  paginationParams,
  readPagination,
} from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';
import { AuthorizationState } from '../auth/authorization.state';

@Component({
  selector: 'crm-ticket-category-list',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    RouterLink,
    ButtonModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './ticket-category-list.html',
  styleUrl: './ticket-category-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketCategoryList {
  private readonly ticketCategoriesService = inject(TicketCategoriesService);
  private readonly departmentsService = inject(DepartmentsService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);
  readonly categories = signal<TicketCategory[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  /** URL-owned page state; see the branches list for the shared rationale. */
  private readonly urlState = injectListUrlState<PagedListState>(readPagination, paginationParams);

  protected readonly page = computed(() => this.urlState.state().page);
  protected readonly pageSize = computed(() => this.urlState.state().pageSize);
  protected readonly first = computed(() => (this.page() - 1) * this.pageSize());

  constructor() {
    effect(() => {
      const state = this.urlState.state();
      void this.load(state.page, state.pageSize);
    });
  }

  private readonly departments = signal<Department[]>([]);
  protected readonly departmentNamesById = computed<ReadonlyMap<string, string>>(
    () => new Map(this.departments().map((department) => [department.id, department.englishName])),
  );

  async load(page = this.page(), pageSize = this.pageSize()): Promise<void> {
    this.loading.set(true);
    try {
      const [result, departments] = await Promise.all([
        this.ticketCategoriesService.list(page, pageSize),
        this.departments().length
          ? Promise.resolve({ items: this.departments() })
          : this.departmentsService.list(1, 200),
      ]);
      this.categories.set([...result.items]);
      this.totalRecords.set(result.totalCount);
      this.departments.set([...departments.items]);
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

  async toggleActive(category: TicketCategory): Promise<void> {
    if (category.isActive) {
      await this.ticketCategoriesService.deactivate(category.id);
    } else {
      await this.ticketCategoriesService.activate(category.id);
    }
    await this.load();
  }
}
