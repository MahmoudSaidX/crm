import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { BranchesService } from './branches.service';
import { LocalizationService, TranslationKey } from '@squad-crm/platform';
import { AgentLanguageSwitcher } from '../i18n/agent-language-switcher';

@Component({
  selector: 'crm-branch-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    TextareaModule,
    AgentLanguageSwitcher,
  ],
  templateUrl: './branch-form.html',
  styleUrl: './branch-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BranchForm {
  private readonly branchesService = inject(BranchesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localization = inject(LocalizationService);

  private branchId: string | null = null;
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
      this.branchId = id;
      this.isEdit.set(true);
      void this.loadBranch(id);
    }
  }

  private async loadBranch(id: string): Promise<void> {
    const branch = await this.branchesService.get(id);
    this.form.setValue({
      code: branch.code,
      arabicName: branch.arabicName,
      englishName: branch.englishName,
      description: branch.description ?? '',
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

      if (this.branchId) {
        await this.branchesService.update(this.branchId, request);
      } else {
        await this.branchesService.create(request);
      }

      await this.router.navigateByUrl('/branches');
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
    if (code === 'branches.duplicate_code') {
      return 'branches.errors.duplicateCode';
    }
    return 'common.errors.generic';
  }
}
