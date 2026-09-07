import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type CustomerPreferredLanguage = 'Arabic' | 'English';

export type CustomerStatus = 'Active' | 'Inactive';

export interface Customer {
  readonly id: string;
  readonly customerNumber: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly preferredLanguage: CustomerPreferredLanguage | null;
  readonly departmentId: string | null;
  readonly branchId: string | null;
  readonly status: CustomerStatus;
  readonly version: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

export interface CreateCustomerRequest {
  readonly firstName: string;
  readonly lastName: string;
  readonly preferredLanguage: CustomerPreferredLanguage | null;
  readonly departmentId: string | null;
  readonly branchId: string | null;
}

export interface UpdateCustomerRequest {
  readonly firstName: string;
  readonly lastName: string;
  readonly preferredLanguage: CustomerPreferredLanguage | null;
  readonly departmentId: string | null;
  readonly branchId: string | null;
  readonly status: CustomerStatus;
  readonly version: number;
}

export type CustomerSortBy = 'CustomerNumber' | 'FirstName' | 'LastName' | 'CreatedAtUtc';
export type SortDirection = 'Asc' | 'Desc';

export interface CustomerListQuery {
  readonly search?: string;
  readonly departmentIds?: readonly string[];
  readonly branchIds?: readonly string[];
  readonly status?: readonly Customer['status'][];
  readonly sortBy?: CustomerSortBy;
  readonly sortDirection?: SortDirection;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export type CustomerContactType = 'Email' | 'Phone';

export interface CustomerContact {
  readonly id: string;
  readonly customerId: string;
  readonly type: CustomerContactType;
  readonly value: string;
  readonly label: string | null;
  readonly isPrimary: boolean;
  readonly isActive: boolean;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

export interface AddCustomerContactRequest {
  readonly type: CustomerContactType;
  readonly value: string;
  readonly label: string | null;
  readonly isPrimary: boolean;
}

export interface UpdateCustomerContactRequest {
  readonly value: string;
  readonly label: string | null;
  readonly isPrimary: boolean;
}

export interface CustomerNote {
  readonly id: string;
  readonly customerId: string;
  readonly body: string;
  readonly authorUserId: string;
  readonly createdAtUtc: string;
}

export interface AddCustomerNoteRequest {
  readonly body: string;
}

export interface CustomerAttachment {
  readonly id: string;
  readonly customerId: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly description: string | null;
  readonly uploadedBy: string;
  readonly uploadedAtUtc: string;
}

export type CustomerTimelineVisibility = 'Internal' | 'Customer';

export interface CustomerTimelineEvent {
  readonly eventType: string;
  readonly occurredAtUtc: string;
  readonly actorDisplay: string | null;
  readonly relatedEntityType: string;
  readonly relatedEntityId: string;
  readonly summary: string;
  readonly visibility: CustomerTimelineVisibility;
}

/**
 * Covers create/list/detail/update (CRM-122/123/124/125), contact
 * management (CRM-126), notes (CRM-127), attachments (CRM-128) and the
 * interaction history timeline (CRM-129).
 */
@Injectable({ providedIn: 'root' })
export class CustomersService {
  private readonly http = inject(HttpClient);

  create(request: CreateCustomerRequest): Promise<Customer> {
    return firstValueFrom(this.http.post<Customer>('/api/v1/customers', request));
  }

  list(query: CustomerListQuery, page: number, pageSize: number): Promise<PagedResult<Customer>> {
    let params: Record<string, string | readonly string[]> = {
      page: String(page),
      pageSize: String(pageSize),
    };
    if (query.search) {
      params = { ...params, search: query.search };
    }
    if (query.departmentIds?.length) {
      params = { ...params, departmentIds: query.departmentIds };
    }
    if (query.branchIds?.length) {
      params = { ...params, branchIds: query.branchIds };
    }
    if (query.status?.length) {
      params = { ...params, status: query.status };
    }
    if (query.sortBy) {
      params = { ...params, sortBy: query.sortBy };
    }
    if (query.sortDirection) {
      params = { ...params, sortDirection: query.sortDirection };
    }
    return firstValueFrom(this.http.get<PagedResult<Customer>>('/api/v1/customers', { params }));
  }

  get(id: string): Promise<Customer> {
    return firstValueFrom(this.http.get<Customer>(`/api/v1/customers/${id}`));
  }

  update(id: string, request: UpdateCustomerRequest): Promise<Customer> {
    return firstValueFrom(this.http.put<Customer>(`/api/v1/customers/${id}`, request));
  }

  listContacts(customerId: string): Promise<CustomerContact[]> {
    return firstValueFrom(
      this.http.get<CustomerContact[]>(`/api/v1/customers/${customerId}/contacts`),
    );
  }

  addContact(customerId: string, request: AddCustomerContactRequest): Promise<CustomerContact> {
    return firstValueFrom(
      this.http.post<CustomerContact>(`/api/v1/customers/${customerId}/contacts`, request),
    );
  }

  updateContact(
    customerId: string,
    contactId: string,
    request: UpdateCustomerContactRequest,
  ): Promise<CustomerContact> {
    return firstValueFrom(
      this.http.put<CustomerContact>(
        `/api/v1/customers/${customerId}/contacts/${contactId}`,
        request,
      ),
    );
  }

  deactivateContact(
    customerId: string,
    contactId: string,
    newPrimaryContactId: string | null,
  ): Promise<CustomerContact> {
    return firstValueFrom(
      this.http.post<CustomerContact>(
        `/api/v1/customers/${customerId}/contacts/${contactId}/deactivate`,
        { newPrimaryContactId },
      ),
    );
  }

  listNotes(customerId: string): Promise<CustomerNote[]> {
    return firstValueFrom(this.http.get<CustomerNote[]>(`/api/v1/customers/${customerId}/notes`));
  }

  addNote(customerId: string, request: AddCustomerNoteRequest): Promise<CustomerNote> {
    return firstValueFrom(
      this.http.post<CustomerNote>(`/api/v1/customers/${customerId}/notes`, request),
    );
  }

  listAttachments(customerId: string): Promise<CustomerAttachment[]> {
    return firstValueFrom(
      this.http.get<CustomerAttachment[]>(`/api/v1/customers/${customerId}/attachments`),
    );
  }

  uploadAttachment(
    customerId: string,
    file: File,
    description: string | null,
  ): Promise<CustomerAttachment> {
    const form = new FormData();
    form.append('file', file, file.name);
    const params: Record<string, string> = description ? { description } : {};
    return firstValueFrom(
      this.http.post<CustomerAttachment>(`/api/v1/customers/${customerId}/attachments`, form, {
        params,
      }),
    );
  }

  removeAttachment(customerId: string, attachmentId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`/api/v1/customers/${customerId}/attachments/${attachmentId}`),
    );
  }

  getTimeline(customerId: string): Promise<CustomerTimelineEvent[]> {
    return firstValueFrom(
      this.http.get<CustomerTimelineEvent[]>(`/api/v1/customers/${customerId}/timeline`),
    );
  }

  downloadAttachment(customerId: string, attachmentId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`/api/v1/customers/${customerId}/attachments/${attachmentId}`, {
        responseType: 'blob',
      }),
    );
  }
}
