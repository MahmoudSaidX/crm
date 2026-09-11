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
import { PaginatorModule } from 'primeng/paginator';
import {
  TicketDetail as TicketDetailModel,
  TicketInternalNote,
  TicketTimelineEntry,
  TicketWatcher,
  TicketsService,
} from './tickets.service';
import { TicketEscalationTargetType, TicketStatus } from '../ticket-create/ticket-create.service';
import { AuthorizationState } from '../auth/authorization.state';
import { StaffUser, StaffUsersService } from '../staff-users/staff-users.service';
import {
  Customer,
  CustomerContact,
  CustomerTimelineEvent,
  CustomersService,
} from '../customers/customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { DetailGrid, PageContainer, PageHeader, StatePanel } from '@squad-crm/shared-ui';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { MultiSelectModule } from 'primeng/multiselect';

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
 * CRM-138 adds the manual escalation action in the same inline-form shape,
 * gated on `tickets.escalate`. Escalation renders in its own section, never
 * merged into the status section: escalation is not a lifecycle status (BR).
 * The escalation LEVEL is never sent — the backend derives it.
 *
 * CRM-139 adds the read-only history timeline section. It is reloaded after
 * every successful action on this screen, because each of those appends an
 * entry. A failed history load is isolated: the section explains itself and the
 * rest of the ticket still renders.
 *
 * CRM-142 adds a read-only customer context panel, reusing the existing
 * Customer Management reads (`get`, `listContacts`, `getTimeline`) — all
 * already gated on `customers.view` server-side. The panel renders only when
 * the customer fetch itself succeeds; a caller without `customers.view` sees
 * the ticket without the panel, never a "restricted" placeholder, so missing
 * access never leaks the existence of fields it can't see.
 *
 * CRM-147 adds the internal collaboration panel — append-only notes with
 * validated teammate mentions, and the ticket's watcher list — gated on
 * `tickets.collaborate` for writes (UX only; the backend authorizes every
 * call). It adds NO handoff control: handing a ticket to another agent is the
 * existing assignment action above and handing it to a department is the
 * existing escalation action, because a second ownership path would contradict
 * the story's own Business Rule.
 *
 * Not built here, because the capabilities they belong to do not exist yet:
 * customer-facing conversation (CRM-164+) and SLA state (CRM-149/150). Each is
 * a separate story that will add its own section and its own backend
 * authorization.
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
    PaginatorModule,
    CardModule,
    DialogModule,
    MultiSelectModule,
    PageContainer,
    PageHeader,
    DetailGrid,
    StatePanel,
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

  /**
   * Null until the customer read succeeds. The whole context panel is gated
   * on this signal rather than a separate permission check, so a caller who
   * cannot read the customer sees no panel at all — not a placeholder that
   * would leak the customer's existence.
   */
  readonly customer = signal<Customer | null>(null);
  readonly customerDepartmentName = signal<string | null>(null);
  readonly customerBranchName = signal<string | null>(null);
  readonly customerContacts = signal<readonly CustomerContact[]>([]);
  readonly customerRecentActivity = signal<readonly CustomerTimelineEvent[]>([]);

  private static readonly customerRecentActivityLimit = 5;

  readonly customerPrimaryEmail = computed(
    () => this.primaryContact(this.customerContacts(), 'Email')?.value ?? null,
  );
  readonly customerPrimaryPhone = computed(
    () => this.primaryContact(this.customerContacts(), 'Phone')?.value ?? null,
  );

  /** Page size of the history section; the backend caps page size at 200. */
  protected readonly historyPageSize = 10;

  readonly history = signal<readonly TicketTimelineEntry[]>([]);
  readonly historyPage = signal(1);
  readonly historyTotal = signal(0);
  readonly historyUnavailable = signal(false);

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

  readonly escalating = signal(false);
  readonly submittingEscalation = signal(false);
  readonly escalationErrorKey = signal<TranslationKey | null>(null);

  /**
   * Empty when the caller lacks the owning module's view permission or the
   * lookup failed — the form then explains why no target can be picked instead
   * of showing an empty select with no reason.
   */
  readonly escalationTargetOptions = signal<{ readonly label: string; readonly value: string }[]>(
    [],
  );
  readonly escalationTargetsUnavailable = signal(false);

  readonly escalationForm = new FormGroup({
    targetType: new FormControl<TicketEscalationTargetType>('Agent', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    targetId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(500)],
    }),
  });

  readonly escalationTargetTypeOptions = computed(() =>
    (['Agent', 'Department'] as const).map((targetType) => ({
      label: this.localization.translate(
        `tickets.escalation.targetTypes.${targetType}` as TranslationKey,
      ),
      value: targetType,
    })),
  );

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

  /** Page size of the notes and watchers sections; the backend caps at 200. */
  protected readonly notesPageSize = 10;

  readonly notes = signal<readonly TicketInternalNote[]>([]);
  readonly notesPage = signal(1);
  readonly notesTotal = signal(0);
  readonly notesUnavailable = signal(false);
  readonly addingNote = signal(false);
  readonly submittingNote = signal(false);
  readonly noteErrorKey = signal<TranslationKey | null>(null);

  readonly watchers = signal<readonly TicketWatcher[]>([]);
  readonly watchersUnavailable = signal(false);
  readonly addingWatcher = signal(false);
  readonly submittingWatcher = signal(false);
  readonly watcherErrorKey = signal<TranslationKey | null>(null);

  readonly noteForm = new FormGroup({
    body: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(4000)],
    }),
    mentionedUserIds: new FormControl<string[]>([], { nonNullable: true }),
  });

  readonly watcherForm = new FormGroup({
    userId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
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

  /** Dialog close (mask, escape, close button) maps onto the existing cancel flow. */
  onStatusDialogVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancelStatusChange();
    }
  }

  onAssignDialogVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancelAssignment();
    }
  }

  onEscalationDialogVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancelEscalation();
    }
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

  async startEscalation(): Promise<void> {
    this.escalationErrorKey.set(null);
    this.escalationForm.reset({ targetType: 'Agent', targetId: '', reason: '' });
    this.escalating.set(true);
    await this.loadEscalationTargets('Agent');
  }

  /** Switching the target kind reloads the target list and clears the choice. */
  async onEscalationTargetTypeSelected(targetType: TicketEscalationTargetType): Promise<void> {
    this.escalationForm.controls.targetId.setValue('');
    await this.loadEscalationTargets(targetType);
  }

  cancelEscalation(): void {
    this.escalating.set(false);
    this.escalationErrorKey.set(null);
  }

  async submitEscalation(): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }

    const reason = this.escalationForm.controls.reason.value.trim();
    const targetId = this.escalationForm.controls.targetId.value;
    if (this.escalationForm.invalid || targetId === '' || reason.length === 0) {
      this.escalationForm.markAllAsTouched();
      this.escalationErrorKey.set(
        targetId === ''
          ? 'tickets.escalation.validation.target'
          : 'tickets.escalation.validation.reason',
      );
      return;
    }

    this.submittingEscalation.set(true);
    this.escalationErrorKey.set(null);
    try {
      await this.ticketsService.escalate(ticket.id, {
        targetType: this.escalationForm.controls.targetType.value,
        targetId,
        reason,
        // The level is NOT sent: the backend derives it from the ticket's
        // current level, so a stale screen cannot corrupt the sequence.
        version: ticket.version,
      });
    } catch (error) {
      // 409: someone else changed the ticket first — reload rather than retry
      // with a version already known to be stale. 422 covers both an
      // ineligible ticket and an inactive target; the response code
      // distinguishes them.
      const response = error instanceof HttpErrorResponse ? error : null;
      const code = response?.error?.code as string | undefined;
      this.escalationErrorKey.set(
        response?.status === 409
          ? 'tickets.escalation.errors.staleVersion'
          : code === 'tickets.not_escalatable'
            ? 'tickets.escalation.errors.notEscalatable'
            : code === 'tickets.invalid_escalation_target'
              ? 'tickets.escalation.errors.invalidTarget'
              : 'tickets.escalation.errors.failed',
      );
      this.submittingEscalation.set(false);
      return;
    }

    this.submittingEscalation.set(false);
    this.escalating.set(false);
    // Reload: the new level, target, timestamp and version are all
    // server-decided.
    await this.load(ticket.id);
  }

  private async loadEscalationTargets(targetType: TicketEscalationTargetType): Promise<void> {
    try {
      const options =
        targetType === 'Agent'
          ? (await this.staffUsersService.list(1, 100)).items
              .filter((user: StaffUser) => user.isActive)
              .map((user: StaffUser) => ({
                label: user.displayName ?? user.email,
                value: user.id,
              }))
          : (await this.departmentsService.list(1, 100)).items
              .filter((department) => department.isActive)
              .map((department) => ({
                label:
                  this.localizedName(department.arabicName, department.englishName) ??
                  department.id,
                value: department.id,
              }));
      this.escalationTargetOptions.set(options);
      this.escalationTargetsUnavailable.set(false);
    } catch {
      this.escalationTargetOptions.set([]);
      this.escalationTargetsUnavailable.set(true);
    }
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

  /** `p-paginator` reports a 0-based first-row offset; the API pages from 1. */
  async onHistoryPageChange(first: number): Promise<void> {
    await this.loadHistory(Math.floor(first / this.historyPageSize) + 1);
  }

  protected historyEventLabel(eventType: string): string {
    return this.localization.translate(`tickets.history.events.${eventType}` as TranslationKey);
  }

  private async loadHistory(page: number): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }
    try {
      const result = await this.ticketsService.history(ticket.id, page, this.historyPageSize);
      this.history.set(result.items);
      this.historyPage.set(result.page);
      this.historyTotal.set(result.totalCount);
      this.historyUnavailable.set(false);
    } catch {
      // Isolated from the ticket itself: a history failure must not blank the
      // screen, so the section says so and the rest keeps rendering.
      this.history.set([]);
      this.historyTotal.set(0);
      this.historyUnavailable.set(true);
    }
  }

  /** `p-paginator` reports a 0-based first-row offset; the API pages from 1. */
  async onNotesPageChange(first: number): Promise<void> {
    await this.loadNotes(Math.floor(first / this.notesPageSize) + 1);
  }

  async startAddNote(): Promise<void> {
    this.noteErrorKey.set(null);
    this.noteForm.reset({ body: '', mentionedUserIds: [] });
    this.addingNote.set(true);
    await this.loadAgentOptions();
  }

  onNoteDialogVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancelAddNote();
    }
  }

  cancelAddNote(): void {
    this.addingNote.set(false);
    this.noteErrorKey.set(null);
  }

  async submitNote(): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }

    const body = this.noteForm.controls.body.value.trim();
    if (this.noteForm.invalid || body.length === 0) {
      this.noteForm.markAllAsTouched();
      this.noteErrorKey.set('tickets.notes.validation.body');
      return;
    }

    this.submittingNote.set(true);
    this.noteErrorKey.set(null);
    try {
      await this.ticketsService.addNote(ticket.id, {
        body,
        mentionedUserIds: this.noteForm.controls.mentionedUserIds.value,
      });
    } catch (error) {
      // 422 means a mentioned user is not an active user — the backend rejects
      // the whole note rather than dropping that mention, so the author is
      // never left believing a teammate was notified.
      const status = error instanceof HttpErrorResponse ? error.status : 0;
      this.noteErrorKey.set(
        status === 422 ? 'tickets.notes.errors.ineligibleUser' : 'tickets.notes.errors.failed',
      );
      this.submittingNote.set(false);
      return;
    }

    this.submittingNote.set(false);
    this.addingNote.set(false);
    // Reload both: the note appears in the notes list AND as a history entry.
    await Promise.all([this.loadNotes(1), this.loadHistory(1)]);
  }

  async startAddWatcher(): Promise<void> {
    this.watcherErrorKey.set(null);
    this.watcherForm.reset({ userId: '' });
    this.addingWatcher.set(true);
    await this.loadAgentOptions();
  }

  onWatcherDialogVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancelAddWatcher();
    }
  }

  cancelAddWatcher(): void {
    this.addingWatcher.set(false);
    this.watcherErrorKey.set(null);
  }

  async submitWatcher(): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }

    if (this.watcherForm.invalid) {
      this.watcherForm.markAllAsTouched();
      this.watcherErrorKey.set('tickets.watchers.validation.user');
      return;
    }

    this.submittingWatcher.set(true);
    this.watcherErrorKey.set(null);
    try {
      await this.ticketsService.addWatcher(ticket.id, {
        userId: this.watcherForm.controls.userId.value,
      });
    } catch (error) {
      const status = error instanceof HttpErrorResponse ? error.status : 0;
      this.watcherErrorKey.set(
        status === 422
          ? 'tickets.watchers.errors.ineligibleUser'
          : 'tickets.watchers.errors.failed',
      );
      this.submittingWatcher.set(false);
      return;
    }

    this.submittingWatcher.set(false);
    this.addingWatcher.set(false);
    await Promise.all([this.loadWatchers(), this.loadHistory(1)]);
  }

  async removeWatcher(userId: string): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }

    this.watcherErrorKey.set(null);
    try {
      await this.ticketsService.removeWatcher(ticket.id, userId);
    } catch {
      this.watcherErrorKey.set('tickets.watchers.errors.removeFailed');
      return;
    }
    await Promise.all([this.loadWatchers(), this.loadHistory(1)]);
  }

  /**
   * Resolves a user id to a display label from the agent lookup when it is
   * loaded, falling back to the raw id — the same "never silently blank"
   * approach the reference labels above use.
   */
  protected userLabel(userId: string): string {
    return this.agentOptions().find((option) => option.value === userId)?.label ?? userId;
  }

  private async loadNotes(page: number): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }
    try {
      const result = await this.ticketsService.listNotes(ticket.id, page, this.notesPageSize);
      this.notes.set(result.items);
      this.notesPage.set(result.page);
      this.notesTotal.set(result.totalCount);
      this.notesUnavailable.set(false);
    } catch {
      // Isolated like the history section: a notes failure must not blank the
      // screen.
      this.notes.set([]);
      this.notesTotal.set(0);
      this.notesUnavailable.set(true);
    }
  }

  private async loadWatchers(): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) {
      return;
    }
    try {
      const result = await this.ticketsService.listWatchers(ticket.id, 1, 100);
      this.watchers.set(result.items);
      this.watchersUnavailable.set(false);
    } catch {
      this.watchers.set([]);
      this.watchersUnavailable.set(true);
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
    await Promise.all([
      this.loadReferenceLabels(ticket),
      this.loadHistory(1),
      this.loadCustomerContext(ticket.customerId),
      this.loadNotes(1),
      this.loadWatchers(),
      // Loaded up front, not only when an action dialog opens, because the
      // watcher list and note mention chips resolve ids to names through it.
      // It fails softly: a caller without `users.view` sees raw ids.
      this.loadAgentOptions(),
    ]);
  }

  /**
   * The customer fetch gates the whole panel: if it fails (403/404), the
   * contacts and timeline calls are skipped and every context signal stays
   * empty, so the panel renders nothing rather than a "restricted" state.
   */
  private async loadCustomerContext(customerId: string): Promise<void> {
    let customer: Customer;
    try {
      customer = await this.customersService.get(customerId);
    } catch {
      this.customer.set(null);
      this.customerDepartmentName.set(null);
      this.customerBranchName.set(null);
      this.customerContacts.set([]);
      this.customerRecentActivity.set([]);
      return;
    }
    this.customer.set(customer);

    await Promise.all([
      this.resolve(
        () =>
          customer.departmentId
            ? this.departmentsService.get(customer.departmentId)
            : Promise.reject(),
        (department) => this.localizedName(department.arabicName, department.englishName),
        this.customerDepartmentName,
      ),
      this.resolve(
        () => (customer.branchId ? this.branchesService.get(customer.branchId) : Promise.reject()),
        (branch) => this.localizedName(branch.arabicName, branch.englishName),
        this.customerBranchName,
      ),
      (async () => {
        try {
          this.customerContacts.set(
            (await this.customersService.listContacts(customerId)).filter(
              (contact) => contact.isActive,
            ),
          );
        } catch {
          this.customerContacts.set([]);
        }
      })(),
      (async () => {
        try {
          const timeline = await this.customersService.getTimeline(customerId);
          this.customerRecentActivity.set(
            [...timeline]
              .sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc))
              .slice(0, TicketDetail.customerRecentActivityLimit),
          );
        } catch {
          this.customerRecentActivity.set([]);
        }
      })(),
    ]);
  }

  private primaryContact(
    contacts: readonly CustomerContact[],
    type: CustomerContact['type'],
  ): CustomerContact | null {
    const matching = contacts.filter((contact) => contact.type === type);
    return matching.find((contact) => contact.isPrimary) ?? matching[0] ?? null;
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

  protected escalationTargetTypeLabel(targetType: TicketEscalationTargetType): string {
    return this.localization.translate(
      `tickets.escalation.targetTypes.${targetType}` as TranslationKey,
    );
  }

  protected statusLabel(status: TicketStatus): string {
    return this.localization.translate(`tickets.statuses.${status}` as TranslationKey);
  }

  protected customerStatusLabel(status: Customer['status']): string {
    return this.localization.translate(
      `tickets.customerContext.statuses.${status}` as TranslationKey,
    );
  }

  private localizedName(arabicName: string | null, englishName: string | null): string | null {
    const preferred = this.localization.locale() === 'ar' ? arabicName : englishName;
    return preferred ?? arabicName ?? englishName;
  }
}
