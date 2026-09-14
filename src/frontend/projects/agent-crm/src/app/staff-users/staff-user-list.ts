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
import { ParamMap, Params, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule, TablePageEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { StaffUser, StaffUsersService } from './staff-users.service';
import {
  LocalizationService,
  injectListUrlState,
  paginationParams,
  readPagination,
  readText,
  toQueryParams,
} from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';
import { AuthorizationState } from '../auth/authorization.state';

interface StaffUserListState {
  readonly page: number;
  readonly pageSize: number;
  readonly search: string;
}

function parseState(params: ParamMap): StaffUserListState {
  return { ...readPagination(params), search: readText(params, 'search') };
}

function stateToParams(state: StaffUserListState): Params {
  return { ...paginationParams(state), ...toQueryParams({ search: state.search }) };
}

@Component({
  selector: 'crm-staff-user-list',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    RouterLink,
    FormsModule,
    ButtonModule,
    InputTextModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './staff-user-list.html',
  styleUrl: './staff-user-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffUserList {
  private readonly staffUsersService = inject(StaffUsersService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);
  readonly staffUsers = signal<StaffUser[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly search = signal('');

  /** URL-owned list state; `search` above stays the submit-based draft. */
  private readonly urlState = injectListUrlState<StaffUserListState>(parseState, stateToParams);

  protected readonly page = computed(() => this.urlState.state().page);
  protected readonly pageSize = computed(() => this.urlState.state().pageSize);
  protected readonly first = computed(() => (this.page() - 1) * this.pageSize());

  constructor() {
    effect(() => {
      const state = this.urlState.state();
      this.search.set(state.search);
      void this.load(state);
    });
  }

  async load(state: StaffUserListState = this.urlState.state()): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.staffUsersService.list(
        state.page,
        state.pageSize,
        state.search || undefined,
      );
      this.staffUsers.set([...result.items]);
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

  onSearch(): void {
    this.urlState.patch({ search: this.search() });
  }

  async toggleActive(staffUser: StaffUser): Promise<void> {
    if (staffUser.isActive) {
      await this.staffUsersService.deactivate(staffUser.id);
    } else {
      await this.staffUsersService.activate(staffUser.id);
    }
    await this.load();
  }
}
