import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { StaffUser, StaffUsersService } from './staff-users.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';

@Component({
  selector: 'crm-staff-user-list',
  imports: [
    RouterLink,
    FormsModule,
    ButtonModule,
    InputTextModule,
    TableModule,
    TagModule,
    AgentLanguageSwitcher,
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
  readonly pageSize = 20;

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.staffUsersService.list(
        page,
        this.pageSize,
        this.search() || undefined,
      );
      this.staffUsers.set([...result.items]);
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

  onSearch(): void {
    void this.load(1);
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
