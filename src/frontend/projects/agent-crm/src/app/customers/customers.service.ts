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

/**
 * Create-only: view/edit/list/contact details/notes/attachments/interaction
 * history are added by later stories (CRM-123/124/125/126/127/128/129).
 */
@Injectable({ providedIn: 'root' })
export class CustomersService {
  private readonly http = inject(HttpClient);

  create(request: CreateCustomerRequest): Promise<Customer> {
    return firstValueFrom(this.http.post<Customer>('/api/v1/customers', request));
  }
}
