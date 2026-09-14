import { DatePipe } from '@angular/common';
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
import { AuditRecord, AuditService } from './audit.service';
import {
  LocalizationService,
  injectListUrlState,
  paginationParams,
  readPagination,
  readText,
  toQueryParams,
} from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

interface AuditListState {
  readonly page: number;
  readonly pageSize: number;
  readonly entityType: string;
  readonly action: string;
  readonly actorHandle: string;
}

function parseState(params: ParamMap): AuditListState {
  return {
    ...readPagination(params),
    entityType: readText(params, 'entityType'),
    action: readText(params, 'action'),
    actorHandle: readText(params, 'actorHandle'),
  };
}

function stateToParams(state: AuditListState): Params {
  return {
    ...paginationParams(state),
    ...toQueryParams({
      entityType: state.entityType,
      action: state.action,
      actorHandle: state.actorHandle,
    }),
  };
}

@Component({
  selector: 'crm-audit-list',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    RouterLink,
    FormsModule,
    ButtonModule,
    InputTextModule,
    TableModule,
    DatePipe,
  ],
  templateUrl: './audit-list.html',
  styleUrl: './audit-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditList {
  private readonly auditService = inject(AuditService);
  protected readonly localization = inject(LocalizationService);
  readonly auditRecords = signal<AuditRecord[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly entityType = signal('');
  readonly action = signal('');
  readonly actorHandle = signal('');

  /** URL-owned list state; the filter signals above stay the submit-based draft. */
  private readonly urlState = injectListUrlState<AuditListState>(parseState, stateToParams);

  protected readonly page = computed(() => this.urlState.state().page);
  protected readonly pageSize = computed(() => this.urlState.state().pageSize);
  protected readonly first = computed(() => (this.page() - 1) * this.pageSize());

  constructor() {
    effect(() => {
      const state = this.urlState.state();
      this.entityType.set(state.entityType);
      this.action.set(state.action);
      this.actorHandle.set(state.actorHandle);
      void this.load(state);
    });
  }

  async load(state: AuditListState): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.auditService.list(state.page, state.pageSize, {
        entityType: state.entityType || undefined,
        action: state.action || undefined,
        actorHandle: state.actorHandle || undefined,
      });
      this.auditRecords.set([...result.items]);
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
      entityType: this.entityType(),
      action: this.action(),
      actorHandle: this.actorHandle(),
    });
  }
}
