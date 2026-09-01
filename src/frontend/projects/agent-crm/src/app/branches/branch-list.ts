import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Branch, BranchesService } from './branches.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';

@Component({
  selector: 'crm-branch-list',
  imports: [RouterLink, ButtonModule, TableModule, TagModule, AgentLanguageSwitcher],
  templateUrl: './branch-list.html',
  styleUrl: './branch-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BranchList {
  private readonly branchesService = inject(BranchesService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);
  readonly branches = signal<Branch[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly pageSize = 20;

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.branchesService.list(page, this.pageSize);
      this.branches.set([...result.items]);
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

  async toggleActive(branch: Branch): Promise<void> {
    if (branch.isActive) {
      await this.branchesService.deactivate(branch.id);
    } else {
      await this.branchesService.activate(branch.id);
    }
    await this.load();
  }
}
