import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import {
  Customer,
  CustomerContact,
  CustomerContactType,
  CustomerPreferredLanguage,
  CustomerStatus,
  CustomersService,
} from './customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';
import { AuthorizationState } from '../auth/authorization.state';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

@Component({
  selector: 'crm-customer-detail',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    ButtonModule,
    CheckboxModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TableModule,
    TagModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './customer-detail.html',
  styleUrl: './customer-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly customersService = inject(CustomersService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly branchesService = inject(BranchesService);
  protected readonly localization = inject(LocalizationService);
  protected readonly authorization = inject(AuthorizationState);

  readonly customer = signal<Customer | null>(null);
  readonly loading = signal(false);
  readonly notFound = signal(false);

  readonly editing = signal(false);
  readonly editSubmitting = signal(false);
  readonly editErrorKey = signal<TranslationKey | null>(null);
  readonly departmentOptions = signal<SelectOption[]>([]);
  readonly branchOptions = signal<SelectOption[]>([]);

  readonly languageOptions: SelectOption[] = [
    { label: this.localization.translate('customers.language.arabic'), value: 'Arabic' },
    { label: this.localization.translate('customers.language.english'), value: 'English' },
  ];

  readonly statusOptions: SelectOption[] = [
    { label: this.localization.translate('common.status.active'), value: 'Active' },
    { label: this.localization.translate('common.status.inactive'), value: 'Inactive' },
  ];

  readonly editForm = new FormGroup({
    firstName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    lastName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    preferredLanguage: new FormControl<CustomerPreferredLanguage | null>(null),
    departmentId: new FormControl<string | null>(null),
    branchId: new FormControl<string | null>(null),
    status: new FormControl<CustomerStatus>('Active', { nonNullable: true }),
  });

  readonly contacts = signal<CustomerContact[]>([]);
  readonly contactsLoading = signal(false);
  readonly showContactForm = signal(false);
  readonly editingContactId = signal<string | null>(null);
  readonly contactErrorKey = signal<TranslationKey | null>(null);
  readonly deactivatingContactId = signal<string | null>(null);

  readonly typeOptions: SelectOption[] = [
    { label: this.localization.translate('customers.contacts.type.email'), value: 'Email' },
    { label: this.localization.translate('customers.contacts.type.phone'), value: 'Phone' },
  ];

  readonly contactForm = new FormGroup({
    type: new FormControl<CustomerContactType>('Email', { nonNullable: true }),
    value: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    label: new FormControl<string | null>(null),
    isPrimary: new FormControl(false, { nonNullable: true }),
  });

  readonly newPrimaryControl = new FormControl<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      return;
    }

    this.loading.set(true);
    try {
      this.customer.set(await this.customersService.get(id));
      await this.loadContacts(id);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.notFound.set(true);
      } else {
        throw error;
      }
    } finally {
      this.loading.set(false);
    }
  }

  startEdit(): void {
    const customer = this.customer();
    if (!customer) {
      return;
    }

    this.editErrorKey.set(null);
    this.editForm.reset({
      firstName: customer.firstName,
      lastName: customer.lastName,
      preferredLanguage: customer.preferredLanguage,
      departmentId: customer.departmentId,
      branchId: customer.branchId,
      status: customer.status,
    });
    this.editing.set(true);
    void this.loadOptions();
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.editErrorKey.set(null);
  }

  private async loadOptions(): Promise<void> {
    const [departments, branches] = await Promise.all([
      this.departmentsService.list(1, 200),
      this.branchesService.list(1, 200),
    ]);
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

  async submitEdit(): Promise<void> {
    const customer = this.customer();
    if (!customer || this.editForm.invalid || this.editSubmitting()) {
      this.editForm.markAllAsTouched();
      return;
    }

    this.editSubmitting.set(true);
    this.editErrorKey.set(null);
    try {
      const raw = this.editForm.getRawValue();
      const updated = await this.customersService.update(customer.id, {
        firstName: raw.firstName,
        lastName: raw.lastName,
        preferredLanguage: raw.preferredLanguage,
        departmentId: raw.departmentId,
        branchId: raw.branchId,
        status: raw.status,
        version: customer.version,
      });
      this.customer.set(updated);
      this.editing.set(false);
    } catch (error) {
      if (
        error instanceof HttpErrorResponse &&
        (error.error as { code?: string })?.code === 'customers.update_conflict'
      ) {
        this.customer.set(await this.customersService.get(customer.id));
      }
      this.editErrorKey.set(this.resolveEditErrorKey(error));
    } finally {
      this.editSubmitting.set(false);
    }
  }

  private resolveEditErrorKey(error: unknown): TranslationKey {
    if (!(error instanceof HttpErrorResponse)) {
      return 'common.errors.generic';
    }

    const code = (error.error as { code?: string } | null)?.code;
    if (code === 'customers.inactive_department') {
      return 'customers.errors.inactiveDepartment';
    }
    if (code === 'customers.inactive_branch') {
      return 'customers.errors.inactiveBranch';
    }
    if (code === 'customers.update_conflict') {
      return 'customers.errors.updateConflict';
    }
    return 'common.errors.generic';
  }

  private async loadContacts(customerId: string): Promise<void> {
    this.contactsLoading.set(true);
    try {
      this.contacts.set(await this.customersService.listContacts(customerId));
    } finally {
      this.contactsLoading.set(false);
    }
  }

  otherActiveContactsOfSameType(contact: CustomerContact): readonly CustomerContact[] {
    return this.contacts().filter(
      (candidate) =>
        candidate.type === contact.type && candidate.isActive && candidate.id !== contact.id,
    );
  }

  newPrimaryOptions(contact: CustomerContact): SelectOption[] {
    return this.otherActiveContactsOfSameType(contact).map((candidate) => ({
      label: candidate.label ? `${candidate.value} (${candidate.label})` : candidate.value,
      value: candidate.id,
    }));
  }

  startAddContact(): void {
    this.editingContactId.set(null);
    this.contactErrorKey.set(null);
    this.contactForm.reset({ type: 'Email', value: '', label: null, isPrimary: false });
    this.showContactForm.set(true);
  }

  startEditContact(contact: CustomerContact): void {
    this.editingContactId.set(contact.id);
    this.contactErrorKey.set(null);
    this.contactForm.reset({
      type: contact.type,
      value: contact.value,
      label: contact.label,
      isPrimary: contact.isPrimary,
    });
    this.showContactForm.set(true);
  }

  cancelContactForm(): void {
    this.showContactForm.set(false);
    this.editingContactId.set(null);
    this.contactErrorKey.set(null);
  }

  async submitContact(): Promise<void> {
    const customer = this.customer();
    if (!customer || this.contactForm.invalid) {
      this.contactForm.markAllAsTouched();
      return;
    }

    this.contactErrorKey.set(null);
    const raw = this.contactForm.getRawValue();
    const editingId = this.editingContactId();
    try {
      if (editingId) {
        await this.customersService.updateContact(customer.id, editingId, {
          value: raw.value,
          label: raw.label,
          isPrimary: raw.isPrimary,
        });
      } else {
        await this.customersService.addContact(customer.id, {
          type: raw.type,
          value: raw.value,
          label: raw.label,
          isPrimary: raw.isPrimary,
        });
      }
      this.showContactForm.set(false);
      this.editingContactId.set(null);
      await this.loadContacts(customer.id);
    } catch (error) {
      this.contactErrorKey.set(this.resolveContactErrorKey(error));
    }
  }

  requestDeactivateContact(contact: CustomerContact): void {
    if (contact.isPrimary && this.otherActiveContactsOfSameType(contact).length > 0) {
      this.newPrimaryControl.reset(null);
      this.deactivatingContactId.set(contact.id);
      return;
    }

    void this.deactivateContact(contact, null);
  }

  cancelDeactivate(): void {
    this.deactivatingContactId.set(null);
  }

  async confirmDeactivateWithNewPrimary(contact: CustomerContact): Promise<void> {
    await this.deactivateContact(contact, this.newPrimaryControl.value);
  }

  private async deactivateContact(
    contact: CustomerContact,
    newPrimaryContactId: string | null,
  ): Promise<void> {
    const customer = this.customer();
    if (!customer) {
      return;
    }

    this.contactErrorKey.set(null);
    try {
      await this.customersService.deactivateContact(customer.id, contact.id, newPrimaryContactId);
      this.deactivatingContactId.set(null);
      await this.loadContacts(customer.id);
    } catch (error) {
      this.contactErrorKey.set(this.resolveContactErrorKey(error));
    }
  }

  private resolveContactErrorKey(error: unknown): TranslationKey {
    if (!(error instanceof HttpErrorResponse)) {
      return 'common.errors.generic';
    }

    const code = (error.error as { code?: string } | null)?.code;
    if (code === 'customers.contacts.invalid_value') {
      return 'customers.contacts.errors.invalidValue';
    }
    if (code === 'customers.contacts.requires_new_primary') {
      return 'customers.contacts.errors.requiresNewPrimary';
    }
    if (code === 'customers.contacts.invalid_new_primary') {
      return 'customers.contacts.errors.invalidNewPrimary';
    }
    return 'common.errors.generic';
  }
}
