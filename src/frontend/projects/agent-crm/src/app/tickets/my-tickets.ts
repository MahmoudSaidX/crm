import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SortDirection, TicketListQuery, TicketSortBy, TicketsService } from './tickets.service';
import { Ticket, TicketStatus } from '../ticket-create/ticket-create.service';
import { TicketCategoriesService } from '../ticket-categories/ticket-categories.service';
import { TicketPrioritiesService } from '../ticket-priorities/ticket-priorities.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

const STATUS_OPTIONS: readonly TicketStatus[] = [
  'Open',
  'InProgress',
  'PendingCustomer',
  'PendingInternal',
  'Resolved',
  'Closed',
];

interface SortOption {
  readonly sortBy: TicketSortBy;
  readonly sortDirection: SortDirection;
}

const SORT_OPTIONS: Readonly<Record<string, SortOption>> = {
  updatedDesc: { sortBy: 'UpdatedAtUtc', sortDirection: 'Desc' },
  updatedAsc: { sortBy: 'UpdatedAtUtc', sortDirection: 'Asc' },
  createdDesc: { sortBy: 'CreatedAtUtc', sortDirection: 'Desc' },
  ticketNumberAsc: { sortBy: 'TicketNumber', sortDirection: 'Asc' },
};

/**
 * The signed-in agent's own ticket queue (CRM-141).
 *
 * The screen never sends an agent id — it sets `assignedToMe`, and the backend
 * resolves the caller from the authenticated principal. That keeps the queue
 * unspoofable and keeps the canonical `AssignedAgentId` the single source of
 * assignment (BR); no separate "my tickets" store exists.
 *
 * Persisted ticket state is authoritative: the screen reloads on demand
 * (filter, page or the explicit refresh action) rather than tracking a
 * realtime channel, which does not exist in this repo yet.
 */
@Component({
  selector: 'crm-my-tickets',
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './my-tickets.html',
  styleUrl: './my-tickets.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyTickets {
  private readonly ticketsService = inject(TicketsService);
  private readonly ticketCategoriesService = inject(TicketCategoriesService);
  private readonly ticketPrioritiesService = inject(TicketPrioritiesService);
  protected readonly localization = inject(LocalizationService);

  readonly tickets = signal<Ticket[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);

  /** Set when the queue could not be loaded at all — distinct from an empty queue. */
  readonly loadFailed = signal(false);

  readonly search = signal('');
  readonly status = signal<TicketStatus | null>(null);
  readonly categoryId = signal<string | null>(null);
  readonly priorityId = signal<string | null>(null);
  readonly sort = signal<string>('updatedDesc');
  readonly categoryOptions = signal<SelectOption[]>([]);
  readonly priorityOptions = signal<SelectOption[]>([]);
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

  /** Localized lifecycle status label, reused from the browse screen (CRM-137). */
  protected statusLabel(status: TicketStatus): string {
    return this.localization.translate(`tickets.statuses.${status}` as TranslationKey);
  }

  protected sortLabel(value: string): string {
    return this.localization.translate(`tickets.mine.sort.${value}` as TranslationKey);
  }

  constructor() {
    void this.loadFilterOptions();
  }

  /**
   * Filter option lookups are best-effort: a failure leaves the dropdowns
   * empty rather than blanking the queue, which is the screen's actual job.
   */
  private async loadFilterOptions(): Promise<void> {
    try {
      const [categories, priorities] = await Promise.all([
        this.ticketCategoriesService.list(1, 200),
        this.ticketPrioritiesService.list(1, 200),
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
    } catch {
      this.categoryOptions.set([]);
      this.priorityOptions.set([]);
    }
  }

  async load(page = 1): Promise<void> {
    this.page = page;
    this.loading.set(true);
    const sort = SORT_OPTIONS[this.sort()] ?? SORT_OPTIONS['updatedDesc'];
    try {
      const query: TicketListQuery = {
        assignedToMe: true,
        search: this.search() || undefined,
        statuses: this.status() ? [this.status()!] : undefined,
        categoryIds: this.categoryId() ? [this.categoryId()!] : undefined,
        priorityIds: this.priorityId() ? [this.priorityId()!] : undefined,
        sortBy: sort.sortBy,
        sortDirection: sort.sortDirection,
      };
      const result = await this.ticketsService.list(query, page, this.pageSize);
      this.tickets.set([...result.items]);
      this.totalRecords.set(result.totalCount);
      this.loadFailed.set(false);
    } catch {
      this.tickets.set([]);
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
