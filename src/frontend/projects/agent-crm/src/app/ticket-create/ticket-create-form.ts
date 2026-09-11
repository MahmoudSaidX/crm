import { CardModule } from 'primeng/card';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { TicketChannel, TicketCreateService } from './ticket-create.service';
import { Customer, CustomersService } from '../customers/customers.service';
import { TicketCategoriesService } from '../ticket-categories/ticket-categories.service';
import { TicketPrioritiesService } from '../ticket-priorities/ticket-priorities.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

@Component({
  selector: 'crm-ticket-create-form',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    ReactiveFormsModule,
    AutoCompleteModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TextareaModule,
  ],
  templateUrl: './ticket-create-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketCreateForm {
  private readonly ticketCreateService = inject(TicketCreateService);
  private readonly customersService = inject(CustomersService);
  private readonly ticketCategoriesService = inject(TicketCategoriesService);
  private readonly ticketPrioritiesService = inject(TicketPrioritiesService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly branchesService = inject(BranchesService);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);

  readonly submitting = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);
  readonly customerSuggestions = signal<Customer[]>([]);
  readonly categoryOptions = signal<SelectOption[]>([]);
  readonly priorityOptions = signal<SelectOption[]>([]);
  readonly departmentOptions = signal<SelectOption[]>([]);
  readonly branchOptions = signal<SelectOption[]>([]);

  readonly channelOptions: SelectOption[] = [
    { label: this.localization.translate('ticketCreate.channels.agent'), value: 'Agent' },
    { label: this.localization.translate('ticketCreate.channels.portal'), value: 'Portal' },
    { label: this.localization.translate('ticketCreate.channels.email'), value: 'Email' },
    { label: this.localization.translate('ticketCreate.channels.whatsApp'), value: 'WhatsApp' },
    { label: this.localization.translate('ticketCreate.channels.liveChat'), value: 'LiveChat' },
    { label: this.localization.translate('ticketCreate.channels.sms'), value: 'SMS' },
    { label: this.localization.translate('ticketCreate.channels.webForm'), value: 'WebForm' },
  ];

  readonly form = new FormGroup({
    customer: new FormControl<Customer | null>(null, { validators: [Validators.required] }),
    subject: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(4000)],
    }),
    categoryId: new FormControl<string | null>(null, { validators: [Validators.required] }),
    priorityId: new FormControl<string | null>(null, { validators: [Validators.required] }),
    departmentId: new FormControl<string | null>(null, { validators: [Validators.required] }),
    branchId: new FormControl<string | null>(null, { validators: [Validators.required] }),
    channel: new FormControl<TicketChannel>('Agent', { nonNullable: true }),
  });

  constructor() {
    void this.loadOptions();
  }

  private async loadOptions(): Promise<void> {
    const [categories, priorities, departments, branches] = await Promise.all([
      this.ticketCategoriesService.list(1, 200),
      this.ticketPrioritiesService.list(1, 200),
      this.departmentsService.list(1, 200),
      this.branchesService.list(1, 200),
    ]);
    this.categoryOptions.set(
      categories.items
        .filter((category) => category.isActive)
        .map((category) => ({ label: category.englishName, value: category.id })),
    );
    this.priorityOptions.set(
      priorities.items
        .filter((priority) => priority.isActive)
        .map((priority) => ({ label: priority.englishName, value: priority.id })),
    );
    this.departmentOptions.set(
      departments.items
        .filter((department) => department.isActive)
        .map((department) => ({ label: department.englishName, value: department.id })),
    );
    this.branchOptions.set(
      branches.items
        .filter((branch) => branch.isActive)
        .map((branch) => ({ label: branch.englishName, value: branch.id })),
    );
  }

  async searchCustomers(event: AutoCompleteCompleteEvent): Promise<void> {
    const query = event.query?.trim();
    if (!query) {
      this.customerSuggestions.set([]);
      return;
    }
    const page = await this.customersService.list({ search: query }, 1, 20);
    this.customerSuggestions.set([...page.items]);
  }

  protected customerLabel(customer: Customer | null | undefined): string {
    return customer
      ? `${customer.firstName} ${customer.lastName} (${customer.customerNumber})`
      : '';
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorKey.set(null);
    try {
      const raw = this.form.getRawValue();
      await this.ticketCreateService.create({
        customerId: raw.customer!.id,
        subject: raw.subject,
        description: raw.description,
        categoryId: raw.categoryId!,
        subcategoryId: null,
        priorityId: raw.priorityId!,
        departmentId: raw.departmentId!,
        branchId: raw.branchId!,
        channel: raw.channel,
        assignedAgentId: null,
      });

      await this.router.navigateByUrl('/');
    } catch (error) {
      this.errorKey.set(this.resolveErrorKey(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private resolveErrorKey(error: unknown): TranslationKey {
    if (!(error instanceof HttpErrorResponse)) {
      return 'common.errors.generic';
    }

    const code = (error.error as { code?: string } | null)?.code;
    if (code === 'tickets.invalid_customer') {
      return 'ticketCreate.errors.invalidCustomer';
    }
    if (code === 'tickets.inactive_category') {
      return 'ticketCreate.errors.inactiveCategory';
    }
    if (code === 'tickets.inactive_priority') {
      return 'ticketCreate.errors.inactivePriority';
    }
    if (code === 'tickets.inactive_department') {
      return 'ticketCreate.errors.inactiveDepartment';
    }
    if (code === 'tickets.inactive_branch') {
      return 'ticketCreate.errors.inactiveBranch';
    }
    if (code === 'tickets.duplicate_ticket_number') {
      return 'ticketCreate.errors.duplicateTicketNumber';
    }
    return 'common.errors.generic';
  }
}
