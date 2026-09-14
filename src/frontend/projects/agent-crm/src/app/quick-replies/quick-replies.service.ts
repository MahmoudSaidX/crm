import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type QuickReplyScope = 'Global' | 'Personal';

export interface QuickReply {
  readonly id: string;
  readonly name: string;
  readonly arabicContent: string | null;
  readonly englishContent: string | null;
  readonly scope: QuickReplyScope;
  readonly ownerUserId: string | null;
  readonly isActive: boolean;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

/** Scope is set at creation only — the backend rejects any attempt to change it. */
export interface CreateQuickReplyRequest {
  readonly name: string;
  readonly arabicContent: string | null;
  readonly englishContent: string | null;
  readonly scope: QuickReplyScope;
}

export interface UpdateQuickReplyRequest {
  readonly name: string;
  readonly arabicContent: string | null;
  readonly englishContent: string | null;
}

/**
 * Quick reply templates (CRM-145). The list this returns is already scoped by
 * the backend to global templates plus the caller's own personal ones — the
 * client never asks for an owner, and could not widen the result if it did.
 */
@Injectable({ providedIn: 'root' })
export class QuickRepliesService {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Promise<PagedResult<QuickReply>> {
    return firstValueFrom(
      this.http.get<PagedResult<QuickReply>>('/api/v1/quick-replies', {
        params: { page, pageSize },
      }),
    );
  }

  get(id: string): Promise<QuickReply> {
    return firstValueFrom(this.http.get<QuickReply>(`/api/v1/quick-replies/${id}`));
  }

  create(request: CreateQuickReplyRequest): Promise<QuickReply> {
    return firstValueFrom(this.http.post<QuickReply>('/api/v1/quick-replies', request));
  }

  update(id: string, request: UpdateQuickReplyRequest): Promise<QuickReply> {
    return firstValueFrom(this.http.put<QuickReply>(`/api/v1/quick-replies/${id}`, request));
  }

  activate(id: string): Promise<QuickReply> {
    return firstValueFrom(this.http.post<QuickReply>(`/api/v1/quick-replies/${id}/activate`, {}));
  }

  deactivate(id: string): Promise<QuickReply> {
    return firstValueFrom(this.http.post<QuickReply>(`/api/v1/quick-replies/${id}/deactivate`, {}));
  }
}
