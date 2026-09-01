import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Department, DepartmentsService } from './departments.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';

@Component({
  selector: 'crm-department-list',
  imports: [RouterLink, ButtonModule, TableModule, TagModule, AgentLanguageSwitcher],
  templateUrl: './department-list.html',
  styleUrl: './department-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DepartmentList {
  private readonly departmentsService = inject(DepartmentsService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);
  readonly departments = signal<Department[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly pageSize = 20;

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.departmentsService.list(page, this.pageSize);
      this.departments.set([...result.items]);
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

  async toggleActive(department: Department): Promise<void> {
    if (department.isActive) {
      await this.departmentsService.deactivate(department.id);
    } else {
      await this.departmentsService.activate(department.id);
    }
    await this.load();
  }
}
