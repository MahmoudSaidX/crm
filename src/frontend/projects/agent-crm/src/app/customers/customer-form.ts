import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { CustomerPreferredLanguage, CustomersService } from './customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

@Component({
  selector: 'crm-customer-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './customer-form.html',
  styleUrl: './customer-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerForm {
  private readonly customersService = inject(CustomersService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly branchesService = inject(BranchesService);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);

  readonly submitting = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);
  readonly departmentOptions = signal<SelectOption[]>([]);
  readonly branchOptions = signal<SelectOption[]>([]);

  readonly languageOptions: SelectOption[] = [
    { label: this.localization.translate('customers.language.arabic'), value: 'Arabic' },
    { label: this.localization.translate('customers.language.english'), value: 'English' },
  ];

  readonly form = new FormGroup({
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
  });

  constructor() {
    void this.loadOptions();
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

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorKey.set(null);
    try {
      const raw = this.form.getRawValue();
      await this.customersService.create({
        firstName: raw.firstName,
        lastName: raw.lastName,
        preferredLanguage: raw.preferredLanguage,
        departmentId: raw.departmentId,
        branchId: raw.branchId,
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
    if (code === 'customers.duplicate_customer') {
      return 'customers.errors.duplicateCustomer';
    }
    if (code === 'customers.inactive_department') {
      return 'customers.errors.inactiveDepartment';
    }
    if (code === 'customers.inactive_branch') {
      return 'customers.errors.inactiveBranch';
    }
    return 'common.errors.generic';
  }
}
