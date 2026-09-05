import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type CustomerPreferredLanguage = 'Arabic' | 'English';

export interface Customer {
  readonly id: string;
  readonly customerNumber: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly preferredLanguage: CustomerPreferredLanguage | null;
  readonly departmentId: string | null;
  readonly branchId: string | null;
  readonly status: 'Active';
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

/**
 * Edit/notes/attachments/interaction history are added by later stories
 * (CRM-125/127/128/129); this covers create/list/detail (CRM-122/123/124)
 * plus contact management (CRM-126).
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
}
