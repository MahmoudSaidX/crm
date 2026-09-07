import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TicketCategoriesService } from './ticket-categories.service';
import { DepartmentsService } from '../departments/departments.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

@Component({
  selector: 'crm-ticket-category-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    MessageModule,
    SelectModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './ticket-category-form.html',
  styleUrl: './ticket-category-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketCategoryForm {
  private readonly ticketCategoriesService = inject(TicketCategoriesService);
  private readonly departmentsService = inject(DepartmentsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);

  private categoryId: string | null = null;
  readonly isEdit = signal(false);
  readonly submitting = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);
  readonly departmentOptions = signal<SelectOption[]>([]);

  readonly form = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(64)],
    }),
    arabicName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    englishName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    defaultDepartmentId: new FormControl<string | null>(null),
    sortOrder: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  constructor() {
    void this.loadDepartmentOptions();

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.categoryId = id;
      this.isEdit.set(true);
      void this.loadCategory(id);
    }
  }

  private async loadDepartmentOptions(): Promise<void> {
    const departments = await this.departmentsService.list(1, 200);
    this.departmentOptions.set(
      departments.items
        .filter((department) => department.isActive)
        .map((department) => ({ label: department.englishName, value: department.id })),
    );
  }

  private async loadCategory(id: string): Promise<void> {
    const category = await this.ticketCategoriesService.get(id);
    this.form.setValue({
      code: category.code,
      arabicName: category.arabicName,
      englishName: category.englishName,
      defaultDepartmentId: category.defaultDepartmentId,
      sortOrder: category.sortOrder,
    });
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
      const request = {
        code: raw.code,
        arabicName: raw.arabicName,
        englishName: raw.englishName,
        defaultDepartmentId: raw.defaultDepartmentId,
        sortOrder: raw.sortOrder,
      };

      if (this.categoryId) {
        await this.ticketCategoriesService.update(this.categoryId, request);
      } else {
        await this.ticketCategoriesService.create(request);
      }

      await this.router.navigateByUrl('/ticket-categories');
    } catch (error) {
      this.errorKey.set(this.resolveErrorKey(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private resolveErrorKey(error: unknown): TranslationKey {
    if (!(error instanceof HttpErrorResponse) || error.status !== 409) {
      return 'common.errors.generic';
    }

    const code = (error.error as { code?: string } | null)?.code;
    if (code === 'ticketcategories.duplicate_code') {
      return 'ticketCategories.errors.duplicateCode';
    }
    if (code === 'ticketcategories.inactive_department') {
      return 'ticketCategories.errors.inactiveDepartment';
    }
    return 'common.errors.generic';
  }
}
