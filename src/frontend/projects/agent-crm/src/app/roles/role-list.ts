import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Role, RolesService } from './roles.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

@Component({
  selector: 'crm-role-list',
  imports: [RouterLink, ButtonModule, TableModule, TagModule, AgentLanguageSwitcher],
  templateUrl: './role-list.html',
  styleUrl: './role-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoleList {
  private readonly rolesService = inject(RolesService);
  protected readonly localization = inject(LocalizationService);
  readonly roles = signal<Role[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly pageSize = 20;

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.rolesService.list(page, this.pageSize);
      this.roles.set([...result.items]);
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

  async toggleActive(role: Role): Promise<void> {
    if (role.isActive) {
      await this.rolesService.deactivate(role.id);
    } else {
      await this.rolesService.activate(role.id);
    }
    await this.load();
  }
}
