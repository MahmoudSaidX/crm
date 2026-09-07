import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TicketPriority, TicketPrioritiesService } from './ticket-priorities.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';

@Component({
  selector: 'crm-ticket-priority-list',
  imports: [RouterLink, ButtonModule, TableModule, TagModule, AgentLanguageSwitcher],
  templateUrl: './ticket-priority-list.html',
  styleUrl: './ticket-priority-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketPriorityList {
  private readonly ticketPrioritiesService = inject(TicketPrioritiesService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);
  readonly priorities = signal<TicketPriority[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly pageSize = 20;

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.ticketPrioritiesService.list(page, this.pageSize);
      this.priorities.set([...result.items]);
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

  async toggleActive(priority: TicketPriority): Promise<void> {
    if (priority.isActive) {
      await this.ticketPrioritiesService.deactivate(priority.id);
    } else {
      await this.ticketPrioritiesService.activate(priority.id);
    }
    await this.load();
  }
}
