import { DatePipe } from '@angular/common';
import { CardModule } from 'primeng/card';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { AuditRecord, AuditService } from './audit.service';
import { LocalizationService } from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

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
  readonly pageSize = 20;

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.auditService.list(page, this.pageSize, {
        entityType: this.entityType() || undefined,
        action: this.action() || undefined,
        actorHandle: this.actorHandle() || undefined,
      });
      this.auditRecords.set([...result.items]);
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
