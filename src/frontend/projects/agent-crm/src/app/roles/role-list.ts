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
import { Role, RolesService } from './roles.service';
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
  selector: 'crm-role-list',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    RouterLink,
    ButtonModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './role-list.html',
  styleUrl: './role-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoleList {
  private readonly rolesService = inject(RolesService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);
  readonly roles = signal<Role[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  /**
   * The URL is the source of truth for this list's page. Nothing here holds a
   * page number of its own, so refresh, a pasted link and browser Back/Forward
   * all reproduce the same view with no extra code.
   */
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

  async load(page = this.page(), pageSize = this.pageSize()): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.rolesService.list(page, pageSize);
      this.roles.set([...result.items]);
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

  async toggleActive(role: Role): Promise<void> {
    if (role.isActive) {
      await this.rolesService.deactivate(role.id);
    } else {
      await this.rolesService.activate(role.id);
    }
    await this.load();
  }
}
