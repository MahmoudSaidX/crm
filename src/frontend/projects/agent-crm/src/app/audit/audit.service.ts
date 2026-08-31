import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PagedResult } from '../roles/roles.service';

export interface AuditRecord {
  readonly id: number;
  readonly actorHandle: string;
  readonly action: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly metadata: Record<string, string> | null;
  readonly occurredAtUtc: string;
}

export interface AuditRecordFilters {
  readonly entityType?: string;
  readonly action?: string;
  readonly actorHandle?: string;
  readonly from?: string;
  readonly to?: string;
}

/**
 * Audit list/detail is gated by audit.view (CRM-114). Read-only: no
 * create/update/delete endpoint exists for an audit record anywhere in the
 * API — this service only exposes list/get.
 */
@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly http = inject(HttpClient);

  list(
    page: number,
    pageSize: number,
    filters?: AuditRecordFilters,
  ): Promise<PagedResult<AuditRecord>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (filters?.entityType) params['entityType'] = filters.entityType;
    if (filters?.action) params['action'] = filters.action;
    if (filters?.actorHandle) params['actorHandle'] = filters.actorHandle;
    if (filters?.from) params['from'] = filters.from;
    if (filters?.to) params['to'] = filters.to;

    return firstValueFrom(
      this.http.get<PagedResult<AuditRecord>>('/api/v1/audit-records', { params }),
    );
  }

  get(id: string): Promise<AuditRecord> {
    return firstValueFrom(this.http.get<AuditRecord>(`/api/v1/audit-records/${id}`));
  }
}
