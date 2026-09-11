import { CardModule } from 'primeng/card';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { DatePickerModule } from 'primeng/datepicker';
import { TaskCreateService } from './task-create.service';
import { Customer, CustomersService } from '../customers/customers.service';
import { Ticket } from '../ticket-create/ticket-create.service';
import { TicketsService } from '../tickets/tickets.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { PageContainer, PageHeader } from '@squad-crm/shared-ui';

@Component({
  selector: 'crm-task-create-form',
  imports: [
    CardModule,
    PageContainer,
    PageHeader,
    ReactiveFormsModule,
    AutoCompleteModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    TextareaModule,
    DatePickerModule,
  ],
  templateUrl: './task-create-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskCreateForm {
  private readonly taskCreateService = inject(TaskCreateService);
  private readonly customersService = inject(CustomersService);
  private readonly ticketsService = inject(TicketsService);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);

  readonly submitting = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);
  readonly customerSuggestions = signal<Customer[]>([]);
  readonly ticketSuggestions = signal<Ticket[]>([]);

  readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    details: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(4000)] }),
    ticket: new FormControl<Ticket | null>(null),
    customer: new FormControl<Customer | null>(null),
    dueAtUtc: new FormControl<Date | null>(null),
  });

  async searchCustomers(event: AutoCompleteCompleteEvent): Promise<void> {
    const query = event.query?.trim();
    if (!query) {
      this.customerSuggestions.set([]);
      return;
    }
    const page = await this.customersService.list({ search: query }, 1, 20);
    this.customerSuggestions.set([...page.items]);
  }

  async searchTickets(event: AutoCompleteCompleteEvent): Promise<void> {
    const query = event.query?.trim();
    if (!query) {
      this.ticketSuggestions.set([]);
      return;
    }
    const page = await this.ticketsService.list({ search: query }, 1, 20);
    this.ticketSuggestions.set([...page.items]);
  }

  protected customerLabel(customer: Customer | null | undefined): string {
    return customer
      ? `${customer.firstName} ${customer.lastName} (${customer.customerNumber})`
      : '';
  }

  protected ticketLabel(ticket: Ticket | null | undefined): string {
    return ticket ? `${ticket.ticketNumber} — ${ticket.subject}` : '';
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
      await this.taskCreateService.create({
        title: raw.title,
        details: raw.details.trim().length > 0 ? raw.details : null,
        ownerUserId: null,
        ticketId: raw.ticket?.id ?? null,
        customerId: raw.customer?.id ?? null,
        dueAtUtc: raw.dueAtUtc ? raw.dueAtUtc.toISOString() : null,
      });

      await this.router.navigateByUrl('/my-tasks');
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
    if (code === 'tasks.invalid_ticket') {
      return 'taskCreate.errors.invalidTicket';
    }
    if (code === 'tasks.invalid_customer') {
      return 'taskCreate.errors.invalidCustomer';
    }
    if (code === 'tasks.ineligible_owner') {
      return 'taskCreate.errors.ineligibleOwner';
    }
    return 'common.errors.generic';
  }
}
