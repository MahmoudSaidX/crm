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
import { QuickReply, QuickRepliesService } from './quick-replies.service';
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
  selector: 'crm-quick-reply-list',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    RouterLink,
    ButtonModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './quick-reply-list.html',
  styleUrl: './quick-reply-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuickReplyList {
  private readonly quickRepliesService = inject(QuickRepliesService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);
  readonly quickReplies = signal<QuickReply[]>([]);
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
      const result = await this.quickRepliesService.list(page, pageSize);
      this.quickReplies.set([...result.items]);
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

  /**
   * Mirrors the backend rule so the UI does not offer an action that would be
   * rejected: global templates need the global permission, personal ones are
   * only editable by their owner — and the backend is what actually enforces
   * both (a hidden button is not a control).
   */
  canManage(quickReply: QuickReply): boolean {
    return quickReply.scope === 'Global'
      ? this.authorization.has('quickreplies.manageglobal')
      : this.authorization.has('quickreplies.manage');
  }

  async toggleActive(quickReply: QuickReply): Promise<void> {
    if (quickReply.isActive) {
      await this.quickRepliesService.deactivate(quickReply.id);
    } else {
      await this.quickRepliesService.activate(quickReply.id);
    }
    await this.load();
  }
}
