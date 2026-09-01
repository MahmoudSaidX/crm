import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { DepartmentsService } from './departments.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

@Component({
  selector: 'crm-department-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    TextareaModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './department-form.html',
  styleUrl: './department-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DepartmentForm {
  private readonly departmentsService = inject(DepartmentsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);

  private departmentId: string | null = null;
  readonly isEdit = signal(false);
  readonly submitting = signal(false);
  readonly errorKey = signal<TranslationKey | null>(null);

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
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(1000)],
    }),
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.departmentId = id;
      this.isEdit.set(true);
      void this.loadDepartment(id);
    }
  }

  private async loadDepartment(id: string): Promise<void> {
    const department = await this.departmentsService.get(id);
    this.form.setValue({
      code: department.code,
      arabicName: department.arabicName,
      englishName: department.englishName,
      description: department.description ?? '',
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
        description: raw.description.trim() === '' ? null : raw.description,
      };

      if (this.departmentId) {
        await this.departmentsService.update(this.departmentId, request);
      } else {
        await this.departmentsService.create(request);
      }

      await this.router.navigateByUrl('/departments');
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
    if (code === 'departments.duplicate_code') {
      return 'departments.errors.duplicateCode';
    }
    return 'common.errors.generic';
  }
}
