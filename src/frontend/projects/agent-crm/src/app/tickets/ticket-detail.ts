import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { TicketDetail as TicketDetailModel, TicketsService } from './tickets.service';
import { TicketStatus } from '../ticket-create/ticket-create.service';
import { AuthorizationState } from '../auth/authorization.state';
import { StaffUser, StaffUsersService } from '../staff-users/staff-users.service';
import { CustomersService } from '../customers/customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
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
 * CRM-136 adds the assignment action: an inline form (the pattern
 * `customer-detail` already established for in-place edits) gated on
 * `tickets.assign`. The gate is UX only — the backend authorizes every call
 * independently.
 *
 * CRM-137 adds the status-transition action in the same inline-form shape,
 * gated on `tickets.changestatus`. The offered statuses come from the ticket's
 * own `allowedStatusTransitions` — the client never hardcodes the lifecycle
 * matrix, and the backend re-validates the transition it receives.
 *
 * Not built here, because the capabilities they belong to do not exist yet:
 * ticket history timeline (CRM-139), customer-facing conversation (CRM-164+),
 * internal notes (CRM-147), SLA/escalation state (CRM-149/150/153), and the
 * manual escalation action (CRM-138). Each is a separate story that
 * will add its own section and its own backend authorization.
 */
@Component({
  selector: 'crm-ticket-detail',
  imports: [
    RouterLink,
    DatePipe,
    ReactiveFormsModule,
    ButtonModule,
    MessageModule,
    SelectModule,
    TagModule,
    TextareaModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketDetail {
  private readonly ticketsService = inject(TicketsService);
  private readonly customersService = inject(CustomersService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly branchesService = inject(BranchesService);
  private readonly staffUsersService = inject(StaffUsersService);
  private readonly route = inject(ActivatedRoute);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);

  readonly ticket = signal<TicketDetailModel | null>(null);
  readonly notFound = signal(false);

  /**
   * Null while still loading or when the label could not be resolved — the
   * template falls back to the raw id so a value is never silently blank.
   */
  readonly customerName = signal<string | null>(null);
  readonly departmentName = signal<string | null>(null);
  readonly branchName = signal<string | null>(null);

  readonly assigning = signal(false);
  readonly submittingAssignment = signal(false);
  readonly assignmentErrorKey = signal<TranslationKey | null>(null);

  /**
   * Empty when the caller lacks `users.view` or the lookup failed — the form
   * then explains why no agent can be picked instead of showing an empty
   * select with no reason.
   */
  readonly agentOptions = signal<{ readonly label: string; readonly value: string }[]>([]);
  readonly agentsUnavailable = signal(false);

  readonly changingStatus = signal(false);
  readonly submittingStatus = signal(false);
  readonly statusErrorKey = signal<TranslationKey | null>(null);

  /**
   * Mirrors the status select's current value as a signal so the reason field's
   * required-ness reacts to the selection (the form control itself is not a
   * signal).
   */
  readonly selectedStatus = signal<TicketStatus | ''>('');

  readonly statusForm = new FormGroup({
    targetStatus: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });

  readonly assignForm = new FormGroup({
    targetAgentId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });

  /** A ticket that already has an owner needs a reason to change it (BR). */
  readonly reasonRequired = computed(() => this.ticket()?.assignedAgentId != null);

  /**
   * Options for the status select, straight from what the backend allows for
   * the current status.
   */
  readonly statusOptions = computed(() =>
    (this.ticket()?.allowedStatusTransitions ?? []).map((status) => ({
      label: this.localization.translate(`tickets.statuses.${status}` as TranslationKey),
      value: status,
    })),
  );

  /**
   * Closing and reopening must be justified — the same rule the backend
   * enforces, mirrored here so the field is marked required before submitting.
   */
  readonly statusReasonRequired = computed(() => {
    const current = this.ticket()?.status;
    const target = this.selectedStatus();
    return target === 'Closed' || current === 'Resolved' || current === 'Closed';
  });

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

  startStatusChange(): void {
    this.statusErrorKey.set(null);
    this.statusForm.reset({ targetStatus: '', reason: '' });
    this.selectedStatus.set('');
    this.changingStatus.set(true);
  }

  onStatusSelected(status: TicketStatus | ''): void {
    this.selectedStatus.set(status);
  }

  cancelStatusChange(): void {
    this.changingStatus.set(false);
    this.statusErrorKey.set(null);
  }

  async submitStatusChange(): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }

    const targetStatus = this.statusForm.controls.targetStatus.value as TicketStatus | '';
    const reason = this.statusForm.controls.reason.value.trim();
    if (this.statusForm.invalid || targetStatus === '') {
      this.statusForm.markAllAsTouched();
      this.statusErrorKey.set('tickets.status.validation.status');
      return;
    }
    if (this.statusReasonRequired() && reason.length === 0) {
      this.statusForm.markAllAsTouched();
      this.statusErrorKey.set('tickets.status.validation.reason');
      return;
    }

    this.submittingStatus.set(true);
    this.statusErrorKey.set(null);
    try {
      await this.ticketsService.changeStatus(ticket.id, {
        targetStatus,
        reason: reason.length > 0 ? reason : null,
        version: ticket.version,
      });
    } catch (error) {
      // 409: someone else changed the ticket first — reload rather than retry
      // with a version already known to be stale. 422: the transition is no
      // longer valid from the current status.
      const status = error instanceof HttpErrorResponse ? error.status : 0;
      this.statusErrorKey.set(
        status === 409
          ? 'tickets.status.errors.staleVersion'
          : status === 422
            ? 'tickets.status.errors.invalidTransition'
            : 'tickets.status.errors.failed',
      );
      this.submittingStatus.set(false);
      return;
    }

    this.submittingStatus.set(false);
    this.changingStatus.set(false);
    // Reload: the new status, version and allowed transitions are all
    // server-decided.
    await this.load(ticket.id);
  }

  async startAssignment(): Promise<void> {
    this.assignmentErrorKey.set(null);
    this.assignForm.reset({ targetAgentId: '', reason: '' });
    this.assigning.set(true);
    await this.loadAgentOptions();
  }

  cancelAssignment(): void {
    this.assigning.set(false);
    this.assignmentErrorKey.set(null);
  }

  async submitAssignment(): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }

    const reason = this.assignForm.controls.reason.value.trim();
    if (this.assignForm.invalid || (this.reasonRequired() && reason.length === 0)) {
      this.assignForm.markAllAsTouched();
      this.assignmentErrorKey.set(
        this.reasonRequired() && reason.length === 0
          ? 'tickets.assign.validation.reason'
          : 'tickets.assign.validation.agent',
      );
      return;
    }

    this.submittingAssignment.set(true);
    this.assignmentErrorKey.set(null);
    try {
      await this.ticketsService.assign(ticket.id, {
        targetAgentId: this.assignForm.controls.targetAgentId.value,
        reason: reason.length > 0 ? reason : null,
        version: ticket.version,
      });
    } catch (error) {
      // A 409 means someone else changed the ticket first: the screen reloads
      // rather than retrying with the version it already knows is stale.
      this.assignmentErrorKey.set(
        error instanceof HttpErrorResponse && error.status === 409
          ? 'tickets.assign.errors.staleVersion'
          : 'tickets.assign.errors.failed',
      );
      this.submittingAssignment.set(false);
      return;
    }

    this.submittingAssignment.set(false);
    this.assigning.set(false);
    // Reload rather than patching locally: the new version/updatedAtUtc are
    // server-generated and the next assignment needs the current version.
    await this.load(ticket.id);
  }

  private async loadAgentOptions(): Promise<void> {
    try {
      const page = await this.staffUsersService.list(1, 100);
      this.agentOptions.set(
        page.items
          .filter((user: StaffUser) => user.isActive)
          .map((user: StaffUser) => ({ label: user.displayName ?? user.email, value: user.id })),
      );
      this.agentsUnavailable.set(false);
    } catch {
      this.agentOptions.set([]);
      this.agentsUnavailable.set(true);
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

  protected statusLabel(status: TicketStatus): string {
    return this.localization.translate(`tickets.statuses.${status}` as TranslationKey);
  }

  private localizedName(arabicName: string | null, englishName: string | null): string | null {
    const preferred = this.localization.locale() === 'ar' ? arabicName : englishName;
    return preferred ?? arabicName ?? englishName;
  }
}
