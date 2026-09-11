import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TicketListQuery, TicketsService } from './tickets.service';
import { Ticket, TicketChannel, TicketStatus } from '../ticket-create/ticket-create.service';
import { TicketCategoriesService } from '../ticket-categories/ticket-categories.service';
import { TicketPrioritiesService } from '../ticket-priorities/ticket-priorities.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

interface SelectOption {
  readonly label: string;
  readonly value: string;
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
    FormsModule,
    RouterLink,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './ticket-list.html',
  styleUrl: './ticket-list.scss',
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
  readonly pageSize = 20;

  /** Localized lifecycle status label (CRM-137). */
  protected statusLabel(status: TicketStatus): string {
    return this.localization.translate(`tickets.statuses.${status}` as TranslationKey);
  }

  constructor() {
    void this.loadFilterOptions();
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

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const query: TicketListQuery = {
        search: this.search() || undefined,
        categoryIds: this.categoryId() ? [this.categoryId()!] : undefined,
        priorityIds: this.priorityId() ? [this.priorityId()!] : undefined,
        departmentIds: this.departmentId() ? [this.departmentId()!] : undefined,
        branchIds: this.branchId() ? [this.branchId()!] : undefined,
        channels: this.channel() ? [this.channel()!] : undefined,
      };
      const result = await this.ticketsService.list(query, page, this.pageSize);
      this.tickets.set([...result.items]);
      this.totalRecords.set(result.totalCount);
    } finally {
      this.loading.set(false);
    }
  }

  onLazyLoad(event: TableLazyLoadEvent): void {
    const first = event.first ?? 0;
    const rows = event.rows ?? this.pageSize;
    const page = Math.floor(first / rows) + 1;
    void this.load(page);
  }

  onFilter(): void {
    void this.load(1);
  }
}
