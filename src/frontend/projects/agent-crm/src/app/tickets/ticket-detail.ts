import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TicketDetail as TicketDetailModel, TicketsService } from './tickets.service';
import { CustomersService } from '../customers/customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { LocalizationService } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

/**
 * Read-only ticket detail (CRM-135). Follows the `audit-detail` definition-list
 * precedent rather than the much larger `customer-detail` screen, which
 * aggregates several stories' worth of editable sub-resources.
 *
 * Composition, not duplication: category/priority names arrive resolved from
 * the TicketManagement module (same DbContext), while customer, department and
 * branch labels are fetched from their owning modules' own APIs, each of which
 * authorizes the caller independently. A caller without `customers.view` simply
 * sees the customer id instead of the name — the ticket still renders.
 *
 * Not built here, because the capabilities they belong to do not exist yet:
 * ticket history timeline (CRM-139), customer-facing conversation (CRM-164+),
 * internal notes (CRM-147), SLA/escalation state (CRM-149/150/153), and the
 * lifecycle/assignment/escalation actions (CRM-136/137/138). Each is a separate
 * story that will add its own section and its own backend authorization.
 */
@Component({
  selector: 'crm-ticket-detail',
  imports: [RouterLink, DatePipe, ButtonModule, TagModule, AgentLanguageSwitcher],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketDetail {
  private readonly ticketsService = inject(TicketsService);
  private readonly customersService = inject(CustomersService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly branchesService = inject(BranchesService);
  private readonly route = inject(ActivatedRoute);
  protected readonly localization = inject(LocalizationService);

  readonly ticket = signal<TicketDetailModel | null>(null);
  readonly notFound = signal(false);

  /**
   * Null while still loading or when the label could not be resolved — the
   * template falls back to the raw id so a value is never silently blank.
   */
  readonly customerName = signal<string | null>(null);
  readonly departmentName = signal<string | null>(null);
  readonly branchName = signal<string | null>(null);

  readonly categoryName = computed(() =>
    this.localizedName(
      this.ticket()?.categoryArabicName ?? null,
      this.ticket()?.categoryEnglishName ?? null,
    ),
  );

  readonly priorityName = computed(() =>
    this.localizedName(
      this.ticket()?.priorityArabicName ?? null,
      this.ticket()?.priorityEnglishName ?? null,
    ),
  );

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      void this.load(id);
    }
  }

  private async load(id: string): Promise<void> {
    let ticket: TicketDetailModel;
    try {
      ticket = await this.ticketsService.get(id);
    } catch {
      this.notFound.set(true);
      return;
    }
    this.ticket.set(ticket);
    await this.loadReferenceLabels(ticket);
  }

  /**
   * Each label is resolved independently and failure is swallowed per label:
   * a missing permission or a deleted reference row must not blank out the
   * rest of the ticket.
   */
  private async loadReferenceLabels(ticket: TicketDetailModel): Promise<void> {
    await Promise.all([
      this.resolve(
        () => this.customersService.get(ticket.customerId),
        (customer) => `${customer.firstName} ${customer.lastName}`.trim(),
        this.customerName,
      ),
      this.resolve(
        () => this.departmentsService.get(ticket.departmentId),
        (department) => this.localizedName(department.arabicName, department.englishName),
        this.departmentName,
      ),
      this.resolve(
        () => this.branchesService.get(ticket.branchId),
        (branch) => this.localizedName(branch.arabicName, branch.englishName),
        this.branchName,
      ),
    ]);
  }

  private async resolve<T>(
    fetch: () => Promise<T>,
    toLabel: (value: T) => string | null,
    target: { set(value: string | null): void },
  ): Promise<void> {
    try {
      target.set(toLabel(await fetch()));
    } catch {
      target.set(null);
    }
  }

  private localizedName(arabicName: string | null, englishName: string | null): string | null {
    const preferred = this.localization.locale() === 'ar' ? arabicName : englishName;
    return preferred ?? arabicName ?? englishName;
  }
}
