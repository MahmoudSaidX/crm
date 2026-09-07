import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TicketCategory, TicketCategoriesService } from './ticket-categories.service';
import { Department, DepartmentsService } from '../departments/departments.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';

@Component({
  selector: 'crm-ticket-category-list',
  imports: [RouterLink, ButtonModule, TableModule, TagModule, AgentLanguageSwitcher],
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
  readonly pageSize = 20;

  private readonly departments = signal<Department[]>([]);
  protected readonly departmentNamesById = computed<ReadonlyMap<string, string>>(
    () => new Map(this.departments().map((department) => [department.id, department.englishName])),
  );

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const [result, departments] = await Promise.all([
        this.ticketCategoriesService.list(page, this.pageSize),
        this.departments().length ? Promise.resolve({ items: this.departments() }) : this.departmentsService.list(1, 200),
      ]);
      this.categories.set([...result.items]);
      this.totalRecords.set(result.totalCount);
      this.departments.set([...departments.items]);
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

  async toggleActive(category: TicketCategory): Promise<void> {
    if (category.isActive) {
      await this.ticketCategoriesService.deactivate(category.id);
    } else {
      await this.ticketCategoriesService.activate(category.id);
    }
    await this.load();
  }
}
