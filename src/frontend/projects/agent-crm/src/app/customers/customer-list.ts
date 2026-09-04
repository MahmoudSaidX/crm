import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Customer, CustomersService } from './customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

@Component({
  selector: 'crm-customer-list',
  imports: [
    RouterLink,
    FormsModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './customer-list.html',
  styleUrl: './customer-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerList {
  private readonly customersService = inject(CustomersService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly branchesService = inject(BranchesService);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);

  readonly customers = signal<Customer[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly search = signal('');
  readonly departmentId = signal<string | null>(null);
  readonly branchId = signal<string | null>(null);
  readonly departmentOptions = signal<SelectOption[]>([]);
  readonly branchOptions = signal<SelectOption[]>([]);
  readonly pageSize = 20;

  constructor() {
    void this.loadFilterOptions();
  }

  private async loadFilterOptions(): Promise<void> {
    const [departments, branches] = await Promise.all([
      this.departmentsService.list(1, 200),
      this.branchesService.list(1, 200),
    ]);
    this.departmentOptions.set(
      departments.items.map((department) => ({
        label: department.englishName,
        value: department.id,
      })),
    );
    this.branchOptions.set(
      branches.items.map((branch) => ({ label: branch.englishName, value: branch.id })),
    );
  }

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.customersService.list(
        {
          search: this.search() || undefined,
          departmentIds: this.departmentId() ? [this.departmentId()!] : undefined,
          branchIds: this.branchId() ? [this.branchId()!] : undefined,
        },
        page,
        this.pageSize,
      );
      this.customers.set([...result.items]);
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

  openCustomer(customer: Customer): void {
    void this.router.navigate(['/customers', customer.id]);
  }
}
